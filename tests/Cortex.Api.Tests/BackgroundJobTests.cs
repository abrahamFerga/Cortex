using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cortex.Application.Jobs;
using Cortex.Infrastructure.Jobs;
using Cortex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cortex.Api.Tests;

/// <summary>
/// The background-job primitive end to end: enqueue under a user's identity, the processor restores
/// tenant + user + permissions and executes the registered handler, progress and result are pollable
/// over the API, and tenant isolation holds. The echo handler also asserts the restored identity —
/// the property the whole primitive exists for.
/// </summary>
public sealed class BackgroundJobTests : IAsyncLifetime
{
    private CortexApiFactory _factory = default!;

    public async Task InitializeAsync()
    {
        JobProcessor.PollInterval = TimeSpan.FromMilliseconds(50); // keep test polling snappy
        _factory = new EchoJobFactory();
        using var warmup = _factory.CreateClient();
        (await warmup.GetAsync("/alive")).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private sealed class EchoJobFactory : CortexApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton<IJobHandler, EchoJobHandler>());
        }
    }

    /// <summary>Reports progress, verifies the restored identity, and echoes its arguments.</summary>
    private sealed class EchoJobHandler : IJobHandler
    {
        public string Kind => "test.echo";

        public async Task<string?> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            // The scope must carry the enqueuer's identity — tenant filters and RBAC depend on it.
            var current = context.ScopedServices.GetRequiredService<Cortex.Core.Identity.ICurrentUser>();
            if (current.TenantId != context.TenantId || current.UserId != context.UserId)
            {
                throw new InvalidOperationException("Job scope does not carry the enqueuer's identity.");
            }

            if (!current.HasPermission("chat.use"))
            {
                throw new InvalidOperationException("Job scope did not resolve the user's permissions.");
            }

            await context.ReportProgressAsync(50, "halfway", cancellationToken);
            var args = JsonDocument.Parse(context.ArgumentsJson).RootElement;
            return JsonSerializer.Serialize(new { echoed = args.GetProperty("message").GetString() });
        }
    }

    private HttpClient ClientFor(string role, string subject)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Dev-Tenant", "dev");
        client.DefaultRequestHeaders.Add("X-Dev-Roles", role);
        return client;
    }

    [Fact]
    public async Task Enqueued_job_executes_under_the_enqueuers_identity_and_is_pollable()
    {
        using var client = ClientFor("user", "job-user");
        (await client.GetAsync("/api/platform/me")).EnsureSuccessStatusCode(); // JIT-provision

        // Enqueue from a scope carrying that user (module code would do this inside a tool/endpoint).
        Guid jobId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var context = scope.ServiceProvider.GetRequiredService<Cortex.Infrastructure.Context.RequestContext>();
            var tenant = await db.Tenants.FirstAsync(t => t.Slug == "dev");
            context.SetTenant(tenant.Id);
            var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Subject == "job-user");
            context.SetUser(user.Id, user.Subject, user.DisplayName);
            // In a real request the enricher resolves these; the snapshot captures them for the job.
            context.SetPermissions(["chat.use", "tools.test.*"]);

            var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
            jobId = await queue.EnqueueAsync("test", "test.echo", new { message = "bulk review me" });
        }

        // Poll the public API until the processor completes it.
        JsonElement job = default;
        for (var i = 0; i < 100; i++)
        {
            job = await client.GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}");
            if (job.GetProperty("status").GetString() is "Succeeded" or "Failed")
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.True(
            job.GetProperty("status").GetString() == "Succeeded",
            $"job did not succeed: status={job.GetProperty("status")}, error={job.GetProperty("error")}");
        Assert.Equal(100, job.GetProperty("progress").GetInt32());
        Assert.Contains("bulk review me", job.GetProperty("resultJson").GetString());

        // The owner sees it in their list.
        var mine = await client.GetFromJsonAsync<JsonElement>("/api/jobs/mine");
        Assert.Contains(mine.EnumerateArray(), j => j.GetProperty("id").GetGuid() == jobId);
    }

    [Fact]
    public async Task Jobs_are_tenant_scoped_and_unknown_kinds_fail_cleanly()
    {
        using var client = ClientFor("user", "job-user-2");
        (await client.GetAsync("/api/platform/me")).EnsureSuccessStatusCode();

        Guid unknownKindJob;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var context = scope.ServiceProvider.GetRequiredService<Cortex.Infrastructure.Context.RequestContext>();
            var tenant = await db.Tenants.FirstAsync(t => t.Slug == "dev");
            context.SetTenant(tenant.Id);
            var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Subject == "job-user-2");
            context.SetUser(user.Id, user.Subject, user.DisplayName);
            context.SetPermissions(["chat.use"]);

            var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
            unknownKindJob = await queue.EnqueueAsync("test", "test.no-such-handler", new { });
        }

        // An unregistered kind fails the job (with the reason recorded), never the processor.
        JsonElement job = default;
        for (var i = 0; i < 100; i++)
        {
            job = await client.GetFromJsonAsync<JsonElement>($"/api/jobs/{unknownKindJob}");
            if (job.GetProperty("status").GetString() is "Succeeded" or "Failed")
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.Equal("Failed", job.GetProperty("status").GetString());
        Assert.Contains("no-such-handler", job.GetProperty("error").GetString());

        // A caller from another tenant cannot see the job at all.
        using var foreign = _factory.CreateClient();
        foreign.DefaultRequestHeaders.Add("X-Dev-Subject", "foreign-admin");
        foreign.DefaultRequestHeaders.Add("X-Dev-Tenant", "other-tenant");
        foreign.DefaultRequestHeaders.Add("X-Dev-Roles", "system_admin");
        // (other-tenant doesn't exist in the seeded store; enrichment yields no tenant → filters hide everything)
        var response = await foreign.GetAsync($"/api/jobs/{unknownKindJob}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
