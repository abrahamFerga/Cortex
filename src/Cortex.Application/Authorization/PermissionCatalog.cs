namespace Cortex.Application.Authorization;

/// <summary>Describes a single permission for the security dashboard: its string, category, and intent.</summary>
public sealed record PermissionInfo(string Permission, string Category, string Description);

/// <summary>
/// The catalog of built-in platform permissions, surfaced to the admin/security dashboard so an
/// operator can see — and reason about — every capability the RBAC system can grant. Module tool
/// permissions are discovered separately from each module's manifest and merged in at the endpoint.
/// This is the platform analogue of OpenClaw's explicit, inspectable tool-permission map.
/// </summary>
public static class PermissionCatalog
{
    public const string PlatformCategory = "Platform administration";
    public const string ChatCategory = "Chat & agents";

    /// <summary>Every built-in (non-module) permission, with a human description for the dashboard.</summary>
    public static readonly IReadOnlyList<PermissionInfo> Platform =
    [
        new(Permissions.ManageTenants, PlatformCategory, "Create, edit, and deactivate tenants."),
        new(Permissions.ManageUsers, PlatformCategory, "Provision users and manage their profile and status."),
        new(Permissions.ManageRoles, PlatformCategory, "Assign roles and grant or revoke permissions."),
        new(Permissions.ManageModules, PlatformCategory, "Enable or disable domain modules for a tenant."),
        new(Permissions.ManageAiSettings, PlatformCategory, "Configure the tenant's AI assistant (system prompt, token budget)."),
        new(Permissions.ViewAuditLog, PlatformCategory, "Read the audit log and token-usage telemetry."),
        new(Permissions.UseChat, ChatCategory, "Start conversations and message the agent."),
        new(Permissions.ViewConversations, ChatCategory, "Read existing conversation history."),
        new(Permissions.ManageApprovals, ChatCategory, "Approve or reject side-effecting tool calls (human-in-the-loop)."),
    ];
}
