using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Connectors.Sdk;

/// <summary>
/// A data-source connector — the bridge to where a customer's data already lives (SharePoint,
/// Azure Blob, a DMS, …). Connectors are to data sources what <c>IModule</c> is to domains: a
/// manifest-first plugin the host registers once (<c>AddCortexConnector&lt;T&gt;()</c>) and a tenant
/// admin enables per tenant. Unlike modules (default-on), connectors are <b>default-off</b>: they
/// reach outside the platform boundary, so enabling one is an explicit, audited admin act.
/// </summary>
public interface IConnector
{
    /// <summary>Static capability declaration. Read before any other member is called.</summary>
    public ConnectorManifest Manifest { get; }

    /// <summary>Register the connector's services and tool source into the host container.</summary>
    public void RegisterServices(IServiceCollection services, IConfiguration configuration);
}

/// <summary>How a connector authenticates against its source.</summary>
public enum ConnectorAuthMode
{
    /// <summary>One tenant-level credential (connection string, API key) configured by the admin.</summary>
    Service = 0,

    /// <summary>Each user connects their own account (OAuth); the source enforces its own ACLs per fetch.</summary>
    UserDelegated = 1,
}

/// <summary>
/// The manifest-first declaration of a connector: identity, auth mode, the admin-configurable
/// settings schema (rendered by the admin console's Integrations page), and the tools it
/// contributes to agents once enabled.
/// </summary>
public sealed record ConnectorManifest
{
    /// <summary>Stable lowercase identifier, e.g. "azure-blob", "sharepoint", "local-folder".</summary>
    public required string Id { get; init; }

    /// <summary>Human-facing name shown on the Integrations page.</summary>
    public required string DisplayName { get; init; }

    public required string Description { get; init; }

    public ConnectorAuthMode AuthMode { get; init; } = ConnectorAuthMode.Service;

    /// <summary>Whether the connector can feed the sync/ingestion lane (RAG) in addition to on-demand fetch.</summary>
    public bool SupportsSync { get; init; }

    /// <summary>Optional icon name for the Integrations page.</summary>
    public string? Icon { get; init; }

    /// <summary>
    /// The tenant-level settings the admin configures when enabling the connector. Schema-driven:
    /// the admin console renders these; values marked secret are write-only and protected at rest.
    /// </summary>
    public IReadOnlyList<ConnectorSettingDescriptor> Settings { get; init; } = [];

    /// <summary>Tools this connector exposes to the agent (metadata; executables registered separately).</summary>
    public IReadOnlyList<Cortex.Modules.Sdk.ToolDescriptor> Tools { get; init; } = [];
}

/// <summary>One admin-configurable connector setting (a field on the Integrations page).</summary>
public sealed record ConnectorSettingDescriptor
{
    /// <summary>The settings-dictionary key, e.g. "ConnectionString", "RootPath".</summary>
    public required string Key { get; init; }

    /// <summary>Field label shown in the admin console.</summary>
    public required string Label { get; init; }

    public string? Description { get; init; }

    public bool Required { get; init; }

    /// <summary>
    /// Secret values (keys, connection strings) are write-only through the admin API — reads report
    /// only that a value exists — and are protected at rest.
    /// </summary>
    public bool IsSecret { get; init; }
}
