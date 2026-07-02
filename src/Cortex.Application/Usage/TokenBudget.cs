namespace Cortex.Application.Usage;

/// <summary>
/// The per-conversation token-budget rule (a MAF production guardrail: "token budget enforced per
/// session"). A budget of 0 means unlimited. The check is deny-by-default once the budget is reached,
/// so an unbounded conversation cannot run up arbitrary cost.
/// </summary>
public static class TokenBudget
{
    /// <summary>True when a positive budget has been reached or exceeded by prior consumption.</summary>
    public static bool IsExceeded(long consumedTokens, int budget) =>
        budget > 0 && consumedTokens >= budget;
}
