using System.Text.Json.Serialization;

namespace Gleanvolt.Mcp;

/// <summary>
/// The request bodies, kept flat where the tools are concerned and assembled here. They mirror
/// <c>contract/openapi.json</c> and are checked against it by the test suite, so a contract change
/// breaks this build rather than producing a tool that silently sends a field the API stopped reading.
/// </summary>
internal static class Requests
{
    /// <summary>
    /// What to put in the car and by when — the input to both the quote and a targeted start, which is
    /// what makes "what you quote is what you commit to" true.
    /// </summary>
    internal sealed record Targeted
    {
        [JsonPropertyName("energyWh")]
        public double? EnergyWh { get; init; }

        [JsonPropertyName("targetSocPercent")]
        public double? TargetSocPercent { get; init; }

        [JsonPropertyName("departBy")]
        public required string DepartBy { get; init; }

        [JsonPropertyName("priority")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Priority { get; init; }

        [JsonPropertyName("restSocPercent")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? RestSocPercent { get; init; }

        [JsonPropertyName("editable")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Editable? Editable { get; init; }
    }

    /// <summary>Limits on how the request may be met: when the charger may run, and how much may be bought.</summary>
    internal sealed record Editable
    {
        [JsonPropertyName("planId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PlanId { get; init; }

        [JsonPropertyName("notBefore")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NotBefore { get; init; }

        [JsonPropertyName("notAfter")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NotAfter { get; init; }

        // Deliberately not null-ignored: zero is a real value here and means "sun only".
        [JsonPropertyName("maxGridEnergyWh")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? MaxGridEnergyWh { get; init; }
    }

    /// <summary>How much a fast charge delivers before it stops.</summary>
    internal sealed record FastLimit
    {
        [JsonPropertyName("basis")]
        public required string Basis { get; init; }

        [JsonPropertyName("energyWh")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? EnergyWh { get; init; }

        [JsonPropertyName("targetSocPercent")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? TargetSocPercent { get; init; }

        [JsonPropertyName("departBy")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DepartBy { get; init; }
    }

    /// <summary>Start controlled charging in one of the modes.</summary>
    internal sealed record Start
    {
        [JsonPropertyName("mode")]
        public required string Mode { get; init; }

        [JsonPropertyName("target")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Targeted? Target { get; init; }

        [JsonPropertyName("fast")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public FastLimit? Fast { get; init; }
    }

    /// <summary>Arm or release the home battery's discharge hold.</summary>
    internal sealed record BatteryHold
    {
        [JsonPropertyName("hold")]
        public required bool Hold { get; init; }
    }

    /// <summary>Whether fastNoBattery and solarGrid wait for low tariff; a field left null is left as it is.</summary>
    internal sealed record LowTariffOnly
    {
        [JsonPropertyName("fastNoBattery")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? FastNoBattery { get; init; }

        [JsonPropertyName("solarGrid")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? SolarGrid { get; init; }
    }

    /// <summary>
    /// The tariff settings to store: the selected provider, the shared prices, and providers' own settings
    /// by key. A whole replacement of the selection and the prices, which is why the tool that sends it
    /// reads the current settings first and changes only what it was given.
    /// </summary>
    internal sealed record TariffSettings
    {
        [JsonPropertyName("provider")]
        public required string Provider { get; init; }

        // Not null-ignored: the API reads an absent price as "no price", so the tool always sends what
        // the price should be after the call, the current one included.
        [JsonPropertyName("lowPricePerKwh")]
        public decimal? LowPricePerKwh { get; init; }

        [JsonPropertyName("highPricePerKwh")]
        public decimal? HighPricePerKwh { get; init; }

        [JsonPropertyName("settings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyDictionary<string, object>? Settings { get; init; }
    }

    /// <summary>One low-tariff period within one day, <c>HH:mm</c> to <c>HH:mm</c>; the end may be <c>24:00</c>.</summary>
    internal sealed record TariffPeriod
    {
        [JsonPropertyName("start")]
        public required string Start { get; init; }

        [JsonPropertyName("end")]
        public required string End { get; init; }
    }

    /// <summary>
    /// Assembles the flat parameters the tools expose into the nested body the API takes. Kept in one
    /// place because the quote and the start must build it identically — two doors onto the same
    /// promise cannot disagree about what was asked for.
    /// </summary>
    internal static Targeted ComposeTargeted(
        double? energyWh,
        double? targetSocPercent,
        string departBy,
        string? priority,
        double? restSocPercent,
        string? notBefore,
        string? notAfter,
        double? maxGridEnergyWh,
        string? planId)
    {
        var hasLimits = notBefore is not null || notAfter is not null || maxGridEnergyWh is not null || planId is not null;

        return new Targeted
        {
            EnergyWh = energyWh,
            TargetSocPercent = targetSocPercent,
            DepartBy = departBy,
            Priority = priority,
            RestSocPercent = restSocPercent,
            Editable = hasLimits
                ? new Editable
                {
                    PlanId = planId,
                    NotBefore = notBefore,
                    NotAfter = notAfter,
                    MaxGridEnergyWh = maxGridEnergyWh,
                }
                : null,
        };
    }
}
