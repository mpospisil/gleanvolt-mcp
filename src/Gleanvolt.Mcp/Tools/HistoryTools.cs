using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;

namespace Gleanvolt.Mcp.Tools;

/// <summary>
/// What the installation has already done. All reads, and all bounded — the API caps both the span of
/// a query and the number of sessions it will return, and those caps are stated in the descriptions so
/// the model asks for something answerable the first time.
/// </summary>
[McpServerToolType]
internal static class HistoryTools
{
    [McpServerTool(Name = "gleanvolt_energy_day", Title = "One day's energy", ReadOnly = true, OpenWorld = true)]
    [Description("""
        A whole local day added up: generation, consumption, import, export, and what went into the car.

        Stated in kilowatt-hours, over the installation's own local day — not UTC — so this is the
        figure that matches a bill or a "how did yesterday go" question. Prefer this over the interval
        series whenever the question is about a day rather than about a moment within one.
        """)]
    internal static Task<string> EnergyDay(
        GleanvoltClient client,
        [Description("The local date, as YYYY-MM-DD.")] string date,
        CancellationToken ct) =>
        client.GetAsync(Endpoints.Fill(Endpoints.EnergyDay, "date", date), ct);

    [McpServerTool(Name = "gleanvolt_energy_intervals", Title = "Energy series", ReadOnly = true, OpenWorld = true)]
    [Description("""
        The energy series at recording resolution, in kilowatt-hours per interval — the shape of a day
        rather than its total. Use it to answer "when did the export happen", not "how much was there".

        The window may not exceed 31 days and a longer one is refused, so ask day by day for detail and
        use gleanvolt_energy_day for totals. Omitting both bounds returns the most recent window.
        """)]
    internal static Task<string> EnergyIntervals(
        GleanvoltClient client,
        [Description("Start of the window, ISO-8601 with an offset. Optional.")] string? from = null,
        [Description("End of the window, ISO-8601 with an offset. Optional.")] string? to = null,
        CancellationToken ct = default) =>
        client.GetAsync($"{Endpoints.EnergyIntervals}{Query(("from", from), ("to", to))}", ct);

    [McpServerTool(Name = "gleanvolt_sessions", Title = "Charging sessions", ReadOnly = true, OpenWorld = true)]
    [Description("""
        Charging sessions in a range, newest first: when each ran, how much was delivered, how much of
        it was solar rather than grid, which mode produced it, and what started it.

        The window may not exceed 31 days and at most 500 sessions come back at once. The summary here
        is usually enough; call gleanvolt_session only when a specific session needs its detail.
        """)]
    internal static Task<string> Sessions(
        GleanvoltClient client,
        [Description("Start of the window, ISO-8601 with an offset. Optional.")] string? from = null,
        [Description("End of the window, ISO-8601 with an offset. Optional.")] string? to = null,
        [Description("Most sessions to return, up to 500.")] int? limit = null,
        CancellationToken ct = default) =>
        client.GetAsync(
            $"{Endpoints.Sessions}{Query(("from", from), ("to", to), ("limit", limit?.ToString(CultureInfo.InvariantCulture)))}",
            ct);

    [McpServerTool(Name = "gleanvolt_session", Title = "One session in full", ReadOnly = true, OpenWorld = true)]
    [Description("""
        One charging session in full, by its id — the detail behind a row from gleanvolt_sessions,
        including how the session ended and, for a targeted charge, what was promised against what
        was delivered.
        """)]
    internal static Task<string> Session(
        GleanvoltClient client,
        [Description("The session's id, a GUID, as returned by gleanvolt_sessions.")] string id,
        CancellationToken ct) =>
        client.GetAsync(Endpoints.Fill(Endpoints.Session, "id", id), ct);

    /// <summary>Builds a query string from the parameters that were actually supplied.</summary>
    private static string Query(params (string Name, string? Value)[] parameters)
    {
        var supplied = parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
            .Select(parameter => $"{parameter.Name}={Uri.EscapeDataString(parameter.Value!)}")
            .ToArray();

        return supplied.Length == 0 ? string.Empty : "?" + string.Join("&", supplied);
    }
}
