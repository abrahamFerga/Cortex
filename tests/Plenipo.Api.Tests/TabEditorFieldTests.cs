using Plenipo.Modules.Sdk;
using Xunit;

namespace Plenipo.Api.Tests;

/// <summary>
/// The field contract modules declare. Options carry a value AND a label because canonical
/// identifiers are rarely readable, and a module must be able to say so without every existing
/// `Options: ["checking", "savings"]` turning into ceremony — hence the implicit string
/// conversion, which these tests exist to keep working.
/// </summary>
public class TabEditorFieldTests
{
    [Fact]
    public void A_bare_string_is_an_option_labelled_with_its_own_value()
    {
        // The everyday case: a vocabulary that already reads fine.
        var field = new TabEditorField("type", "Type", Options: ["checking", "savings"]);

        Assert.Equal(["checking", "savings"], field.Options!.Select(o => o.Value));
        Assert.Equal(["checking", "savings"], field.Options!.Select(o => o.Label));
    }

    [Fact]
    public void An_existing_string_array_spreads_into_options()
    {
        // Modules build these lists at startup (currency codes, time zone ids) and pass the array.
        string[] codes = ["USD", "MXN", "EUR"];
        var field = new TabEditorField("currencyCode", "Currency", Options: [.. codes]);

        Assert.Equal(codes, field.Options!.Select(o => o.Value));
    }

    [Fact]
    public void An_identifier_can_store_one_thing_and_read_as_another()
    {
        var field = new TabEditorField(
            "timeZoneId",
            "Time zone",
            Options: [new TabEditorOption("America/Mexico_City", "Mexico City"), "UTC"]);

        // The value the endpoint expects…
        Assert.Equal("America/Mexico_City", field.Options![0].Value);
        // …and what a human should actually read.
        Assert.Equal("Mexico City", field.Options![0].Label);
        // Mixed with bare strings in the same list.
        Assert.Equal("UTC", field.Options![1].Label);
    }

    [Fact]
    public void A_field_declares_nothing_by_default_so_the_shell_keeps_guessing_nothing()
    {
        var field = new TabEditorField("name", "Name");

        Assert.Null(field.Options);
        Assert.Null(field.Default);
        Assert.Null(field.DefaultFrom);
    }

    [Fact]
    public void A_field_can_say_what_only_the_browser_knows()
    {
        var tz = new TabEditorField(
            "timeZoneId", "Time zone", Required: false,
            Options: ["UTC"], Default: "UTC", DefaultFrom: FieldDefaultSources.BrowserTimeZone);
        Assert.Equal("UTC", tz.Default);
        Assert.Equal("browser-timezone", tz.DefaultFrom);

        // Currency guesses from the browser's locale region — a distinct source, same mechanism.
        var currency = new TabEditorField(
            "currencyCode", "Currency", DefaultFrom: FieldDefaultSources.BrowserCurrency);
        Assert.Equal("browser-currency", currency.DefaultFrom);
    }

    [Fact]
    public void A_field_can_carry_a_presentational_group_for_singleton_forms()
    {
        var field = new TabEditorField("timeZoneId", "Time zone", Group: "Currency & locale");
        Assert.Equal("Currency & locale", field.Group);
        // Ungrouped is the default — the table editor ignores it entirely.
        Assert.Null(new TabEditorField("name", "Name").Group);
    }
}
