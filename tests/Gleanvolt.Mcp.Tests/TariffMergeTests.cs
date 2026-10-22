using System.Text.Json;
using Gleanvolt.Mcp.Tools;

namespace Gleanvolt.Mcp.Tests;

/// <summary>
/// gleanvolt_set_tariff changes only what it is given (gleanvolt #273). The API's PUT replaces the
/// selection and both prices, so what has to hold is that the tool sends back everything it was not
/// asked to change — a weekend edit that wiped the prices would be a silent loss of the owner's data.
/// </summary>
public sealed class TariffMergeTests
{
    private static JsonElement Stored(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static readonly JsonElement Night = Stored("""
        {
          "provider": "Table",
          "lowPricePerKwh": 2.10,
          "highPricePerKwh": 4.80,
          "settings": {
            "table": {
              "weekday": [ { "start": "00:00", "end": "06:00" }, { "start": "22:00", "end": "24:00" } ],
              "weekend": [ { "start": "00:00", "end": "08:00" } ]
            },
            "egd": { "code": "A1B4DP1" }
          },
          "warnings": []
        }
        """);

    private static readonly Requests.TariffPeriod[] Afternoon = [new() { Start = "13:00", End = "15:00" }];

    // What the tool would send, as the API would read it.
    private static JsonElement Sent(Requests.TariffSettings body) =>
        JsonSerializer.SerializeToElement(body, GleanvoltClient.Json);

    [Fact]
    public void ChangingOnePriceKeepsEverythingElse()
    {
        var sent = Sent(TariffControlTools.Merge(Night, provider: null, weekday: null, weekend: null, lowPricePerKwh: 1.90m, highPricePerKwh: null));

        Assert.Equal("Table", sent.GetProperty("provider").GetString());
        Assert.Equal(1.90m, sent.GetProperty("lowPricePerKwh").GetDecimal());
        Assert.Equal(4.80m, sent.GetProperty("highPricePerKwh").GetDecimal());

        // No periods given, so no table is sent at all: the API keeps a section that is left out.
        Assert.False(sent.TryGetProperty("settings", out _));
    }

    [Fact]
    public void ChangingTheWeekendKeepsTheWeekdaysAndThePrices()
    {
        var sent = Sent(TariffControlTools.Merge(Night, provider: null, weekday: null, weekend: Afternoon, lowPricePerKwh: null, highPricePerKwh: null));
        var table = sent.GetProperty("settings").GetProperty("table");

        Assert.Equal("13:00", table.GetProperty("weekend")[0].GetProperty("start").GetString());
        Assert.Equal(2, table.GetProperty("weekday").GetArrayLength());
        Assert.Equal("22:00", table.GetProperty("weekday")[1].GetProperty("start").GetString());
        Assert.Equal(2.10m, sent.GetProperty("lowPricePerKwh").GetDecimal());
    }

    [Fact]
    public void OnlyTheTableIsSentSoAnotherProvidersSettingsAreLeftAlone()
    {
        var sent = Sent(TariffControlTools.Merge(Night, provider: null, weekday: Afternoon, weekend: null, lowPricePerKwh: null, highPricePerKwh: null));

        Assert.Equal(["table"], sent.GetProperty("settings").EnumerateObject().Select(section => section.Name));
    }

    [Fact]
    public void PeriodsWithNoProviderSelectTheTable()
    {
        var none = Stored("""{ "provider": "None", "lowPricePerKwh": null, "highPricePerKwh": null, "settings": {} }""");

        var sent = Sent(TariffControlTools.Merge(none, provider: null, weekday: Afternoon, weekend: null, lowPricePerKwh: null, highPricePerKwh: null));

        Assert.Equal("Table", sent.GetProperty("provider").GetString());
        Assert.Equal(0, sent.GetProperty("settings").GetProperty("table").GetProperty("weekend").GetArrayLength());
    }

    [Fact]
    public void ANamedProviderWinsAndPricesStayNullWhenNoneAreStored()
    {
        var none = Stored("""{ "provider": "Table", "settings": {} }""");

        var sent = Sent(TariffControlTools.Merge(none, provider: "None", weekday: null, weekend: null, lowPricePerKwh: null, highPricePerKwh: null));

        Assert.Equal("None", sent.GetProperty("provider").GetString());
        Assert.Equal(JsonValueKind.Null, sent.GetProperty("lowPricePerKwh").ValueKind);
    }

    [Fact]
    public void AnEmptyListClearsIt()
    {
        var sent = Sent(TariffControlTools.Merge(Night, provider: null, weekday: [], weekend: null, lowPricePerKwh: null, highPricePerKwh: null));

        Assert.Equal(0, sent.GetProperty("settings").GetProperty("table").GetProperty("weekday").GetArrayLength());
        Assert.Equal(1, sent.GetProperty("settings").GetProperty("table").GetProperty("weekend").GetArrayLength());
    }
}
