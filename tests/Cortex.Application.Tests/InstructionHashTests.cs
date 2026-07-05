using Cortex.Application.Ai;

namespace Cortex.Application.Tests;

public sealed class InstructionHashTests
{
    [Fact]
    public void Compute_IsDeterministic_LowercaseHex_64Chars()
    {
        var a = InstructionHash.Compute("You are Cortex.");
        var b = InstructionHash.Compute("You are Cortex.");

        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
        Assert.Equal(a, a.ToLowerInvariant());
    }

    [Fact]
    public void Compute_AnyChange_ChangesTheHash()
    {
        // The whole point: a one-character instruction edit is a different provenance identity.
        Assert.NotEqual(
            InstructionHash.Compute("Always cite sources."),
            InstructionHash.Compute("Always cite sources!"));
    }
}
