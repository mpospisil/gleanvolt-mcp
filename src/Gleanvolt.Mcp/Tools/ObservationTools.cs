using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Gleanvolt.Mcp.Tools;

/// <summary>
/// What the installation is and what it is doing. Every tool here is a plain read: nothing in this
/// class touches hardware.
/// </summary>
[McpServerToolType]
internal static class ObservationTools
{
    [McpServerTool(Name = "gleanvolt_overview", Title = "Site and live state", ReadOnly = true, OpenWorld = true)]
    [Description("""
        What this installation is, and what it is doing right now — call this first, before any other
        tool, because everything else assumes its numbers.

        Returns the site description (inverter, battery capacity, charger, array, time zone, the
        configured car) together with the live state (PV, battery, grid and charger power, state of
        charge, the active charge-control mode, whether the battery discharge hold is armed).

        Powers are instantaneous watts signed as the hardware reports them: grid positive is import,
        battery positive is charging. Energies are watt-hours. Timestamps are ISO-8601 with an offset.
        """)]
    internal static async Task<string> Overview(GleanvoltClient client, CancellationToken ct)
    {
        // Two calls behind one tool. They are always wanted together -- a state of charge means little
        // without the pack size, and a charger power means little without the site's maximum -- and a
        // model given them as separate tools spends a round trip working that out every session.
        var site = await client.GetAsync(Endpoints.Site, ct);
        var status = await client.GetAsync(Endpoints.Status, ct);

        return $$"""{"site":{{site}},"status":{{status}}}""";
    }

    [McpServerTool(Name = "gleanvolt_health", Title = "Controller health", ReadOnly = true, OpenWorld = true)]
    [Description("""
        Whether the controller is alive and what it can currently see: how long it has been up, and
        whether the inverter, the charger, the database, the forecast and the car are each reporting.

        Call this when another tool returns stale-looking or missing data, to tell "the controller is
        down" apart from "the controller is fine and the car simply has not phoned home".
        """)]
    internal static Task<string> Health(GleanvoltClient client, CancellationToken ct) =>
        client.GetAsync(Endpoints.Health, ct);

    [McpServerTool(Name = "gleanvolt_forecast", Title = "Solar forecast", ReadOnly = true, OpenWorld = true)]
    [Description("""
        The solar forecast the controller is planning from, as a series of expected PV power over the
        coming period, plus the daily totals it derives.

        This is the forecast the controller itself uses, not a fresh third-party lookup — so it is the
        right thing to quote when explaining why a plan was shaped the way it was.
        """)]
    internal static Task<string> Forecast(
        GleanvoltClient client,
        [Description("Include the accompanying weather series (cloud cover, temperature). Defaults to false.")]
        bool weather = false,
        CancellationToken ct = default) =>
        client.GetAsync($"{Endpoints.Forecast}?weather={(weather ? "true" : "false")}", ct);

    [McpServerTool(Name = "gleanvolt_vehicle", Title = "Car telemetry", ReadOnly = true, OpenWorld = true)]
    [Description("""
        What the car last said about itself: state of charge, whether it is plugged in, and — the field
        that matters most — when that reading was captured.

        A parked car reports when it feels like it, so treat the capture time as part of the answer and
        say how old the reading is rather than presenting it as current. If the reading is hours old,
        a state-of-charge target computed from it will be wrong by however much has happened since.
        """)]
    internal static Task<string> Vehicle(GleanvoltClient client, CancellationToken ct) =>
        client.GetAsync(Endpoints.Vehicle, ct);
}
