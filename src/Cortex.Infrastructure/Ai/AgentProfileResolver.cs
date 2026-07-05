using Cortex.Application.Ai;
using Cortex.Core.Platform;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Ai;

/// <summary>
/// Reads the tenant's default <see cref="AgentProfile"/> for a module (tenant scoping via the
/// global query filter). No row means "no customization" — the manifest instructions apply as-is,
/// so profiles are invisible until an admin creates one.
/// </summary>
public sealed class AgentProfileResolver(PlatformDbContext db) : IAgentProfileResolver
{
    public Task<AgentProfile?> ResolveActiveAsync(string moduleId, CancellationToken cancellationToken = default) =>
        db.AgentProfiles.FirstOrDefaultAsync(p => p.ModuleId == moduleId && p.IsDefault, cancellationToken);
}
