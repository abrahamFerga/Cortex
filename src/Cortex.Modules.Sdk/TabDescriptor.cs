namespace Cortex.Modules.Sdk;

/// <summary>A column in a tab's server-driven data view: which row field to show, and its header.</summary>
public sealed record TabColumn(string Field, string Header);

/// <summary>
/// A navigation tab a module contributes to the dashboard. The React shell builds its sidebar and
/// routes purely from the tabs returned by the API, filtered by the caller's permissions — the
/// frontend hardcodes no domain routes.
/// </summary>
public sealed record TabDescriptor
{
    /// <summary>Stable id within the module (e.g. "cases", "transactions").</summary>
    public required string Id { get; init; }

    /// <summary>Sidebar label.</summary>
    public required string Label { get; init; }

    /// <summary>Client route the tab renders at (e.g. "/finance/transactions").</summary>
    public required string Route { get; init; }

    /// <summary>Optional icon name (resolved by the frontend icon set).</summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Permission required to see and open this tab. <c>null</c> means visible to any user who has
    /// the module enabled. Tabs the user lacks permission for are omitted, not merely hidden.
    /// </summary>
    public string? Permission { get; init; }

    /// <summary>Sort order within the module's sidebar group.</summary>
    public int Order { get; init; }

    /// <summary>
    /// Optional: a GET endpoint returning a JSON array, which the shell renders as a generic table using
    /// <see cref="Columns"/>. Lets a module's list-style tab show real data without shipping any custom UI.
    /// When null, the tab renders a placeholder (or content supplied by the consuming app).
    /// </summary>
    public string? DataEndpoint { get; init; }

    /// <summary>Columns for the <see cref="DataEndpoint"/> table. Empty falls back to the row's own fields.</summary>
    public IReadOnlyList<TabColumn> Columns { get; init; } = [];

    /// <summary>
    /// Optional friendly empty-state message the shell shows when the tab has no <see cref="DataEndpoint"/>
    /// and the consuming app supplies no content (e.g. "Your food diary will appear here."). Gives a tab an
    /// intentional placeholder — useful for a capability that's declared in the manifest but not yet built —
    /// instead of a generic "nothing here" message.
    /// </summary>
    public string? Placeholder { get; init; }
}
