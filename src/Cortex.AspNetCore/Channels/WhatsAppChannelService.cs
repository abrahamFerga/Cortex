using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Cortex.Application.Agents;
using Cortex.Application.Auditing;
using Cortex.Application.Authorization;
using Cortex.Application.Channels;
using Cortex.Core.Platform;
using Cortex.Infrastructure.Context;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cortex.AspNetCore.Channels;

/// <summary>
/// Turns a verified WhatsApp webhook delivery into authorized agent turns. Identity mirrors the HTTP
/// pipeline's <see cref="Identity.RequestEnricher"/> — resolve tenant → JIT-provision the sender as a
/// platform user (subject <c>whatsapp:{phone}</c>, role <c>user</c>) → resolve permissions — so the
/// agent runs with exactly the authority of that user: tool filtering, auditing, token tracking, and
/// the human-in-the-loop approval gate all apply to WhatsApp turns identically to the web UI.
/// </summary>
public sealed class WhatsAppChannelService(
    RequestContext requestContext,
    PlatformDbContext db,
    IPermissionResolver permissionResolver,
    IAuthorizedAgentRunner runner,
    IWhatsAppSender sender,
    IAuditLog auditLog,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppChannelService> logger)
{
    public async Task ProcessAsync(WhatsAppWebhookPayload payload, CancellationToken cancellationToken)
    {
        foreach (var entry in payload.Entries ?? [])
        {
            foreach (var change in entry.Changes ?? [])
            {
                if (!string.Equals(change.Field, "messages", StringComparison.OrdinalIgnoreCase) ||
                    change.Value?.Messages is not { } messages)
                {
                    continue; // delivery statuses and other webhook fields — nothing to answer
                }

                foreach (var message in messages)
                {
                    await ProcessMessageAsync(change.Value, message, cancellationToken);
                }
            }
        }
    }

    private async Task ProcessMessageAsync(
        WhatsAppWebhookPayload.ChangeValue value,
        WhatsAppWebhookPayload.Message message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.From))
        {
            return;
        }

        if (!string.Equals(message.Type, "text", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(message.Text?.Body))
        {
            // Media/location/etc. still deserve an answer — the sender is a real user waiting on a reply.
            await sender.SendTextAsync(
                message.From,
                "Sorry — I can only read text messages for now. Please send your question as text.",
                cancellationToken);
            return;
        }

        var o = options.Value;

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == o.TenantSlug, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            logger.LogError(
                "WhatsApp channel is bound to tenant '{Slug}' which {Reason}; dropping message.",
                o.TenantSlug, tenant is null ? "does not exist" : "is deactivated");
            return;
        }

        // Same population order as the HTTP pipeline: tenant first, so every query below is tenant-scoped.
        requestContext.SetTenant(tenant.Id);

        var displayName = value.Contacts?.FirstOrDefault(c => c.WaId == message.From)?.Profile?.Name
            ?? $"WhatsApp +{message.From}";
        var subject = $"whatsapp:{message.From}";

        var user = await db.Users.FirstOrDefaultAsync(u => u.Subject == subject, cancellationToken);
        if (user is { IsActive: false })
        {
            logger.LogWarning("Deactivated WhatsApp user {Subject} messaged the channel; dropping.", subject);
            return;
        }

        if (user is null)
        {
            user = new User
            {
                TenantId = tenant.Id,
                Subject = subject,
                Email = $"{message.From}@whatsapp.channel",
                DisplayName = displayName,
            };
            user.Roles.Add(new UserRole { TenantId = tenant.Id, UserId = user.Id, Role = Roles.User });
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);

            await auditLog.RecordAuthEventAsync(new AuthAuditEntry
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                Subject = subject,
                UserDisplay = displayName,
                EventType = AuthAuditEventType.UserProvisioned,
            }, cancellationToken);
        }
        else
        {
            user.LastSeenAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        requestContext.SetUser(user.Id, subject, user.DisplayName);

        // No token exists for a webhook caller; permissions come entirely from the user's DB roles/grants.
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], "whatsapp"));
        requestContext.SetPermissions(await permissionResolver.ResolveAsync(principal, cancellationToken));

        if (!requestContext.HasPermission(Permissions.UseChat))
        {
            await sender.SendTextAsync(
                message.From,
                "You don't have access to the assistant yet. Please contact your administrator.",
                cancellationToken);
            return;
        }

        var reply = await RunAgentTurnAsync(message.Text!.Body!, tenant.Id, message.From, cancellationToken);
        if (!string.IsNullOrWhiteSpace(reply))
        {
            await sender.SendTextAsync(message.From, reply, cancellationToken);
        }
    }

    private async Task<string> RunAgentTurnAsync(string text, Guid tenantId, string phone, CancellationToken cancellationToken)
    {
        var request = new AgentRunRequest
        {
            ModuleId = options.Value.ModuleId!,
            // One long-running conversation per phone number, tenant-scoped — the same stable-id scheme
            // the AG-UI endpoint uses for client-owned thread ids.
            ConversationId = ConversationIdForPhone(tenantId, phone),
            Message = text,
        };

        var reply = new StringBuilder();
        var approvals = new List<string>();

        await foreach (var evt in runner.RunAsync(request, cancellationToken))
        {
            switch (evt.Type)
            {
                case AgentStreamEventType.Token when !string.IsNullOrEmpty(evt.Text):
                    reply.Append(evt.Text);
                    break;

                case AgentStreamEventType.ApprovalRequired when evt.ToolName is not null:
                    approvals.Add(evt.ToolName);
                    break;

                case AgentStreamEventType.Error:
                    logger.LogError("WhatsApp agent turn failed: {Error}", evt.Error);
                    return "Sorry, I couldn't process that request. Please try again later.";
            }
        }

        foreach (var tool in approvals)
        {
            reply.Append(reply.Length > 0 ? "\n\n" : string.Empty)
                 .Append("⏳ The action \"").Append(tool)
                 .Append("\" needs approval before it runs. An operator can approve it from the workspace.");
        }

        return reply.ToString().Trim();
    }

    /// <summary>Stable, tenant-scoped conversation id for a phone number, so every message from the same
    /// number continues one conversation (and two tenants can never collide on one row).</summary>
    public static Guid ConversationIdForPhone(Guid tenantId, string phone)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId:N} whatsapp:{phone}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}
