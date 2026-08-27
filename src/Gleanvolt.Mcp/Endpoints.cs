namespace Gleanvolt.Mcp;

/// <summary>
/// Every route this server calls, as the template the OpenAPI document names it by. Here rather than
/// inline in the tools so the test suite can hold each one against <c>contract/openapi.json</c>: a
/// renamed or dropped endpoint then fails this build, instead of becoming a tool that 404s in front
/// of a model that cannot tell a missing route from an empty answer.
/// </summary>
internal static class Endpoints
{
    internal const string Site = "/api/v1/site";

    internal const string Status = "/api/v1/status";

    internal const string Health = "/api/v1/health";

    internal const string Forecast = "/api/v1/forecast";

    internal const string Vehicle = "/api/v1/vehicle";

    internal const string EnergyIntervals = "/api/v1/energy/intervals";

    internal const string EnergyDay = "/api/v1/energy/days/{date}";

    internal const string Sessions = "/api/v1/sessions";

    internal const string Session = "/api/v1/sessions/{id}";

    internal const string QuotePlan = "/api/v1/plans/targeted/preview";

    internal const string Start = "/api/v1/charging/start";

    internal const string StartTargeted = "/api/v1/charging/start/targeted";

    internal const string Stop = "/api/v1/charging/stop";

    internal const string BatteryHold = "/api/v1/battery-hold";

    /// <summary>
    /// Every route above with the verb it is called by — the list the contract test walks. Adding a
    /// tool means adding its route here, which is the point: the guard cannot be forgotten silently.
    /// </summary>
    internal static readonly (string Method, string Template)[] All =
    [
        ("get", Site),
        ("get", Status),
        ("get", Health),
        ("get", Forecast),
        ("get", Vehicle),
        ("get", EnergyIntervals),
        ("get", EnergyDay),
        ("get", Sessions),
        ("get", Session),
        ("post", QuotePlan),
        ("post", Start),
        ("post", StartTargeted),
        ("post", Stop),
        ("put", BatteryHold),
    ];

    /// <summary>Substitutes a single path segment, escaped.</summary>
    internal static string Fill(string template, string name, string value) =>
        template.Replace($"{{{name}}}", Uri.EscapeDataString(value), StringComparison.Ordinal);
}
