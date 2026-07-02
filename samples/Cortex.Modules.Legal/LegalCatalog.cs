namespace Cortex.Modules.Legal;

/// <summary>A standard contract clause: reference data the agent searches and renders.</summary>
public sealed record Clause(string Id, string Title, string Category, string Summary, string Template);

/// <summary>A rendered clause with the parties substituted in.</summary>
public sealed record RenderedClause(string Title, string Category, string Body);

/// <summary>
/// A small library of standard contract clauses. Deterministic reference data so the Legal module is
/// stateless (no DbContext) — the agent searches it and fills templates, exactly like NutritionCatalog.
/// Templates use {PartyA} / {PartyB} placeholders.
/// </summary>
public static class LegalCatalog
{
    public static readonly IReadOnlyList<Clause> Clauses =
    [
        new("confidentiality", "Confidentiality", "Protection",
            "Each party keeps the other's confidential information secret and uses it only for the agreement's purpose.",
            "Each party (the \"Receiving Party\") shall keep confidential all non-public information disclosed by the other party ({PartyA} or {PartyB}) and shall not use it except to perform its obligations under this Agreement."),
        new("indemnification", "Indemnification", "Risk allocation",
            "One party covers the other's losses arising from defined claims.",
            "{PartyA} shall indemnify and hold harmless {PartyB} from any claims, damages, and reasonable legal fees arising out of {PartyA}'s breach of this Agreement or negligent acts."),
        new("limitation-of-liability", "Limitation of Liability", "Risk allocation",
            "Caps each party's liability and excludes indirect damages.",
            "Neither {PartyA} nor {PartyB} shall be liable for indirect or consequential damages, and each party's total liability shall not exceed the fees paid under this Agreement in the twelve months preceding the claim."),
        new("termination", "Termination", "Lifecycle",
            "How and when either party may end the agreement.",
            "Either {PartyA} or {PartyB} may terminate this Agreement on thirty (30) days' written notice, or immediately if the other party materially breaches and fails to cure within fifteen (15) days."),
        new("governing-law", "Governing Law", "General",
            "Which jurisdiction's law governs the contract.",
            "This Agreement between {PartyA} and {PartyB} shall be governed by and construed in accordance with the laws of the agreed jurisdiction, without regard to its conflict-of-laws rules."),
        new("payment-terms", "Payment Terms", "Commercial",
            "When and how invoices are paid.",
            "{PartyB} shall pay {PartyA} within thirty (30) days of receipt of a valid invoice. Undisputed overdue amounts accrue interest at 1.5% per month."),
        new("ip-assignment", "Intellectual Property Assignment", "IP",
            "Assigns ownership of work product to one party.",
            "All work product created by {PartyA} under this Agreement shall be the exclusive property of {PartyB}, and {PartyA} hereby assigns all right, title, and interest in such work product to {PartyB}."),
        new("non-compete", "Non-Compete", "Restrictive covenant",
            "Restricts a party from competing for a defined period and area (enforceability varies by jurisdiction).",
            "For twelve (12) months after termination, {PartyA} shall not engage in a business that directly competes with {PartyB} within the agreed territory, to the extent permitted by applicable law."),
    ];

    /// <summary>Case-insensitive search over title, category, summary, and id.</summary>
    public static IEnumerable<Clause> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var q = query.Trim();
        var exact = Clauses.Where(c => Matches(c, q)).ToArray();
        if (exact.Length > 0)
        {
            return exact;
        }

        // Forgiving fallback so a natural-language query ("draft a confidentiality clause") still matches:
        // match a clause if any significant word (≥ 4 chars, skipping short stop-words) of the query hits one
        // of its searchable fields.
        var words = q.Split([' ', ',', '.', ';', ':', '?', '!', '-', '/', '"', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4)
            .ToArray();
        return Clauses.Where(c => words.Any(w => Matches(c, w))).ToArray();
    }

    private static bool Matches(Clause c, string term) =>
        c.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        c.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        c.Summary.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        c.Id.Contains(term, StringComparison.OrdinalIgnoreCase);

    /// <summary>Renders a clause's template, substituting the two party names.</summary>
    public static RenderedClause Render(Clause clause, string partyA, string partyB)
    {
        var body = clause.Template
            .Replace("{PartyA}", string.IsNullOrWhiteSpace(partyA) ? "Party A" : partyA.Trim(), StringComparison.Ordinal)
            .Replace("{PartyB}", string.IsNullOrWhiteSpace(partyB) ? "Party B" : partyB.Trim(), StringComparison.Ordinal);
        return new RenderedClause(clause.Title, clause.Category, body);
    }
}
