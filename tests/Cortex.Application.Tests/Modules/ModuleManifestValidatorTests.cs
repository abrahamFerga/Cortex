using Cortex.Application.Modules;
using Cortex.Modules.Sdk;

namespace Cortex.Application.Tests.Modules;

/// <summary>
/// Covers the fail-fast validation of a host's module registration: unique module ids, unique tab ids
/// within a module, unique tab routes across all modules, and required tab fields.
/// </summary>
public sealed class ModuleManifestValidatorTests
{
    private static ModuleManifest Module(string id, params TabDescriptor[] tabs) => new()
    {
        Id = id,
        DisplayName = id,
        Version = "1.0.0",
        Tabs = tabs,
    };

    private static TabDescriptor Tab(string id, string route, string? label = null) => new()
    {
        Id = id,
        Label = label ?? id,
        Route = route,
    };

    [Fact]
    public void ValidRegistration_HasNoErrors()
    {
        var errors = ModuleManifestValidator.Validate([
            Module("finance", Tab("transactions", "/finance/transactions"), Tab("budgets", "/finance/budgets")),
            Module("nutrition", Tab("foods", "/nutrition/foods")),
        ]);

        Assert.Empty(errors);
    }

    [Fact]
    public void DuplicateModuleId_IsReported()
    {
        var errors = ModuleManifestValidator.Validate([Module("finance"), Module("finance")]);

        Assert.Contains(errors, e => e.Contains("Duplicate module id 'finance'"));
    }

    [Fact]
    public void EmptyModuleId_IsReported()
    {
        var errors = ModuleManifestValidator.Validate([Module("  ")]);

        Assert.Contains(errors, e => e.Contains("empty id"));
    }

    [Fact]
    public void DuplicateTabId_WithinAModule_IsReported()
    {
        var errors = ModuleManifestValidator.Validate([
            Module("finance", Tab("ledger", "/finance/a"), Tab("ledger", "/finance/b")),
        ]);

        Assert.Contains(errors, e => e.Contains("Duplicate tab id 'ledger'"));
    }

    [Fact]
    public void SameTabId_InDifferentModules_IsAllowed()
    {
        // Each sample module legitimately declares its own module-scoped "chat" tab, for instance.
        var errors = ModuleManifestValidator.Validate([
            Module("finance", Tab("chat", "/finance/chat")),
            Module("nutrition", Tab("chat", "/nutrition/chat")),
        ]);

        Assert.Empty(errors);
    }

    [Fact]
    public void DuplicateTabRoute_AcrossModules_IsReported()
    {
        var errors = ModuleManifestValidator.Validate([
            Module("finance", Tab("a", "/shared")),
            Module("nutrition", Tab("b", "/shared")),
        ]);

        Assert.Contains(errors, e => e.Contains("Duplicate tab route '/shared'"));
    }

    [Fact]
    public void EmptyTabLabelAndRoute_AreReported()
    {
        var errors = ModuleManifestValidator.Validate([
            Module("finance", new TabDescriptor { Id = "t", Label = " ", Route = " " }),
        ]);

        Assert.Contains(errors, e => e.Contains("empty label"));
        Assert.Contains(errors, e => e.Contains("empty route"));
    }

    [Fact]
    public void AdminTab_WithoutPermission_IsReported()
    {
        var module = Module("finance") with
        {
            AdminTabs = [Tab("institutions", "/ext/finance/institutions")],
        };

        var errors = ModuleManifestValidator.Validate([module]);

        Assert.Contains(errors, e => e.Contains("declares no Permission"));
    }

    [Fact]
    public void AdminTabs_WithPermission_AreValid_AndDuplicateIdsAreReported()
    {
        var gated = Tab("institutions", "/ext/finance/institutions") with { Permission = "finance.admin" };
        Assert.Empty(ModuleManifestValidator.Validate([Module("finance") with { AdminTabs = [gated] }]));

        var errors = ModuleManifestValidator.Validate([
            Module("finance") with { AdminTabs = [gated, gated] },
        ]);
        Assert.Contains(errors, e => e.Contains("Duplicate admin tab id 'institutions'"));
    }

    [Fact]
    public void ThrowIfInvalid_ThrowsAggregatedMessage_WhenInvalid()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ModuleManifestValidator.ThrowIfInvalid([Module("dup"), Module("dup")]));

        Assert.Contains("Duplicate module id 'dup'", ex.Message);
    }

    [Fact]
    public void ThrowIfInvalid_DoesNotThrow_WhenValid()
    {
        ModuleManifestValidator.ThrowIfInvalid([Module("finance", Tab("transactions", "/finance/transactions"))]);
    }
}
