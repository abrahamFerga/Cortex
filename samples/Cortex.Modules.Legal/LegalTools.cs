using System.ComponentModel;

namespace Cortex.Modules.Legal;

/// <summary>
/// The Legal module's agent tools. Stateless and deterministic: the agent searches the clause library
/// and renders templates, then narrates. The tools never opine — the module's instructions keep the
/// assistant from giving actual legal advice.
/// </summary>
public sealed class LegalTools
{
    [Description("Search the standard clause library by keyword. Returns matching clauses with their category and a short summary.")]
    public string SearchClauses(
        [Description("Keywords, e.g. 'confidentiality', 'liability', or 'termination'.")] string query)
    {
        var matches = LegalCatalog.Search(query).Take(8).ToList();
        if (matches.Count == 0)
        {
            return $"No clauses match \"{query}\". Try a keyword like 'confidentiality', 'liability', or 'termination'.";
        }

        var lines = matches.Select(c => $"{c.Title} ({c.Category}) — {c.Summary}");
        return $"Found {matches.Count} clause(s): {string.Join(" | ", lines)}.";
    }

    [Description("Draft a standard contract clause from the library, filled in with the two party names. Use search_clauses first if unsure of the clause type.")]
    public string DraftClause(
        [Description("Clause type or keyword, e.g. 'indemnification' or 'governing law'.")] string clauseType,
        [Description("Name of the first party (e.g. the provider/discloser).")] string partyA,
        [Description("Name of the second party (e.g. the client/recipient).")] string partyB)
    {
        var clause = LegalCatalog.Search(clauseType).FirstOrDefault();
        if (clause is null)
        {
            return $"No standard clause matches \"{clauseType}\". Call search_clauses to find an available clause type first.";
        }

        var rendered = LegalCatalog.Render(clause, partyA, partyB);
        return $"{rendered.Title} ({rendered.Category}):\n\n{rendered.Body}\n\nThis is a standard template, not legal advice — have a licensed attorney review before use.";
    }
}
