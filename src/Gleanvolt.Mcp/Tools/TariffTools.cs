using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Gleanvolt.Mcp.Tools;

/// <summary>
/// The grid tariff (gleanvolt #265): what the grid costs when, and the two places it is stored. Plain
/// reads; the tools that change the tariff are in <see cref="TariffControlTools"/>, registered only
/// when writes are.
/// </summary>
[McpServerToolType]
internal static class TariffTools
{
    [McpServerTool(Name = "gleanvolt_tariff", Title = "Grid tariff now and ahead", ReadOnly = true, OpenWorld = true)]
    [Description("""
        What the grid costs when: the rate now (low or high), its price per kWh and when it next
        changes, the low and high stretches as far ahead as the tariff is known (today and tomorrow for
        the owner's table), and whether fastNoBattery and solarGrid are set to wait for low tariff.

        An empty timeline means NO tariff restriction, never "always low": either no tariff is set up
        (provider None), or the selected one cannot answer, which 'unavailable' then explains. Prices
        are per kWh in 'currency' and may be null when the owner has not entered them. With
        lowTariffOnly.tariffKnown false, a flag that is on is ignored and the mode charges whatever the
        hour.
        """)]
    internal static async Task<string> Tariff(GleanvoltClient client, CancellationToken ct)
    {
        // Two calls behind one tool, like the overview: "is it cheap now?" and "will the car wait for
        // it?" are one question to the person asking.
        var tariff = await client.GetAsync(Endpoints.Tariff, ct);
        var lowTariffOnly = await client.GetAsync(Endpoints.LowTariffOnly, ct);

        return $$"""{"tariff":{{tariff}},"lowTariffOnly":{{lowTariffOnly}}}""";
    }

    [McpServerTool(Name = "gleanvolt_tariff_settings", Title = "Stored tariff settings", ReadOnly = true, OpenWorld = true)]
    [Description("""
        The tariff as the owner stored it: which provider is selected, the low and high price per kWh,
        and each provider's own settings by key -- for the 'table' provider, the low-tariff periods for
        weekdays (Mon-Fri) and the weekend (Sat-Sun) as HH:mm pairs, where an end of 24:00 means the end
        of the day and a night tariff is two rows, 22:00-24:00 and 00:00-06:00. Also the providers this
        installation has.

        Read this before changing the tariff, and to answer "what are my low-tariff times?". For what
        the grid costs right now, use gleanvolt_tariff instead.
        """)]
    internal static async Task<string> TariffSettings(GleanvoltClient client, CancellationToken ct)
    {
        var settings = await client.GetAsync(Endpoints.TariffSettings, ct);
        var providers = await client.GetAsync(Endpoints.TariffProviders, ct);

        return $$"""{"settings":{{settings}},"providers":{{providers}}}""";
    }
}

/// <summary>
/// The two tools that change the tariff and what the car does about it. Registered only when
/// GLEANVOLT_MCP_ALLOW_WRITES is true: neither writes to a device itself, but both change how the next
/// poll drives the charger, which is the same promise the charging tools make.
/// </summary>
[McpServerToolType]
internal static class TariffControlTools
{
    [McpServerTool(Name = "gleanvolt_set_low_tariff_only", Title = "Wait for low tariff", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("""
        Make fastNoBattery and/or solarGrid wait for low tariff. This CHANGES HOW THE CAR CHARGES from
        the next poll, a running charge included -- confirm with the person first.

        With it on, fastNoBattery waits through high tariff and charges flat out once low tariff starts,
        and high tariff pauses solarGrid whether or not the sun would cover it. A wait is never taken
        for a full car. Pass only the mode being changed; the other is left as it is. Turning one on
        with no tariff set up is accepted, and the answer's tariffKnown=false says it has nothing to
        act on yet. Does not survive a restart of the controller.
        """)]
    internal static Task<string> SetLowTariffOnly(
        GleanvoltClient client,
        [Description("True makes fastNoBattery wait for low tariff, false lets it charge whatever the hour. Omit to leave it as it is.")]
        bool? fastNoBattery = null,
        [Description("True makes high tariff pause solarGrid, sun or not; false lets it follow the sun at any hour. Omit to leave it as it is.")]
        bool? solarGrid = null,
        CancellationToken ct = default) =>
        client.PutAsync(Endpoints.LowTariffOnly, new Requests.LowTariffOnly { FastNoBattery = fastNoBattery, SolarGrid = solarGrid }, ct);

    [McpServerTool(Name = "gleanvolt_set_tariff", Title = "Change the tariff", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("""
        Change the owner's tariff: select a provider, set the low-tariff periods, set the prices. Applied
        at once, with no restart. This CHANGES WHAT A LOW-TARIFF-ONLY CHARGE WAITS FOR -- read the current
        settings with gleanvolt_tariff_settings, show the person what will change, and confirm first.

        Only what is passed changes; everything else is kept as stored. Periods are HH:mm pairs in
        local time, only the LOW-tariff ones (every other minute is high tariff); an end may be 24:00,
        and a period over midnight is TWO rows, 22:00-24:00 and 00:00-06:00. Passing periods without a
        provider selects 'Table'. A list given replaces that list; the other list is kept. To clear
        a list, pass it empty. Prices are per kWh in the installation's currency.

        A mistake is refused with a 400 that names each field and row ('table.weekday', row 1) and
        stores nothing -- read it, correct the call, and try again.
        """)]
    internal static async Task<string> SetTariff(
        GleanvoltClient client,
        [Description("'None' for no tariff, 'Table' for the owner's periods, or another id from gleanvolt_tariff_settings. Omit to keep the current one, or to select 'Table' when periods are given.")]
        string? provider = null,
        [Description("Low-tariff periods Monday to Friday, each {start, end} as HH:mm. Omit to keep the stored list.")]
        Requests.TariffPeriod[]? weekday = null,
        [Description("Low-tariff periods on Saturday and Sunday, each {start, end} as HH:mm. Omit to keep the stored list.")]
        Requests.TariffPeriod[]? weekend = null,
        [Description("The low-tariff price per kWh. Omit to keep the stored price.")]
        decimal? lowPricePerKwh = null,
        [Description("The high-tariff price per kWh. Omit to keep the stored price.")]
        decimal? highPricePerKwh = null,
        CancellationToken ct = default)
    {
        // Read, then write the whole: the API's PUT replaces the selection and both prices, so a call
        // that changed only the weekend would otherwise wipe the prices.
        var stored = await client.GetAsync(Endpoints.TariffSettings, ct);
        using var current = JsonDocument.Parse(stored);

        if (current.RootElement.TryGetProperty("error", out _))
        {
            return stored;
        }

        var body = Merge(current.RootElement, provider, weekday, weekend, lowPricePerKwh, highPricePerKwh);
        return await client.PutAsync(Endpoints.TariffSettings, body, ct);
    }

    /// <summary>
    /// The settings to send: <paramref name="current"/> as GET /tariff/settings returned it, with only
    /// what was passed changed. Periods for one list keep the other, and periods with no provider select
    /// the table, which is what somebody typing their low-tariff times means.
    /// </summary>
    internal static Requests.TariffSettings Merge(
        JsonElement current,
        string? provider,
        Requests.TariffPeriod[]? weekday,
        Requests.TariffPeriod[]? weekend,
        decimal? lowPricePerKwh,
        decimal? highPricePerKwh)
    {
        var periodsGiven = weekday is not null || weekend is not null;
        var table = current.TryGetProperty("settings", out var sections) && sections.ValueKind == JsonValueKind.Object
            && sections.TryGetProperty("table", out var section)
                ? section
                : default;

        return new Requests.TariffSettings
        {
            Provider = provider ?? (periodsGiven ? "Table" : Text(current, "provider") ?? "None"),
            LowPricePerKwh = lowPricePerKwh ?? Price(current, "lowPricePerKwh"),
            HighPricePerKwh = highPricePerKwh ?? Price(current, "highPricePerKwh"),
            Settings = periodsGiven
                ? new Dictionary<string, object>
                {
                    ["table"] = new Dictionary<string, object>
                    {
                        ["weekday"] = (object?)weekday ?? Stored(table, "weekday"),
                        ["weekend"] = (object?)weekend ?? Stored(table, "weekend"),
                    },
                }
                : null,
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static decimal? Price(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : null;

    // The stored list as it came, so a list not being changed goes back exactly as it was.
    private static object Stored(JsonElement table, string list) =>
        table.ValueKind == JsonValueKind.Object && table.TryGetProperty(list, out var periods) && periods.ValueKind == JsonValueKind.Array
            ? periods.Clone()
            : Array.Empty<Requests.TariffPeriod>();
}
