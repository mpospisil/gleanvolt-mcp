using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Gleanvolt.Mcp.Tools;

/// <summary>
/// The quote. This is the tool the whole surface is built around: it answers "what would happen if"
/// without anything happening, so the model can reason about a charge out loud and put the numbers in
/// front of a person before a single watt is committed.
/// </summary>
[McpServerToolType]
internal static class PlanTools
{
    [McpServerTool(Name = "gleanvolt_quote_plan", Title = "Quote a targeted charge", ReadOnly = true, OpenWorld = true)]
    [Description("""
        Quote a targeted charge WITHOUT starting it: how much sun the plan expects to catch, how much
        grid it would have to buy and when, whether the target is reachable by the deadline at all, and
        what it would fall short by if not.

        Always call this before gleanvolt_start_targeted, and show the person the numbers. The two take
        the same arguments, so what is quoted is exactly what would be committed.

        Say the amount ONE way: either energyKWh or targetSocPercent, never both. A state-of-charge
        target is converted using what the car last reported, so check gleanvolt_vehicle first — a
        stale reading makes a percentage target wrong by however much has happened since.
        """)]
    internal static Task<string> Quote(
        GleanvoltClient client,
        [Description("When the energy has to be in the car. ISO-8601 WITH an offset (e.g. 2026-08-28T07:30:00+02:00) — this is a local-time promise, and a bare instant is a way to be an hour wrong.")]
        string departBy,
        [Description("The energy to deliver, measured at the charger — not what reaches the cells. Omit when asking in state of charge.")]
        double? energyKWh = null,
        [Description("The state of charge to reach, 0-100, converted from what the car last reported. Omit when asking in kilowatt-hours.")]
        double? targetSocPercent = null,
        [Description("'cheapest' (default) paces the charge across the whole window and takes every watt of sun above that pace, usually meeting the target well before the deadline. 'justInTime' holds the last stretch back so the car reaches its target shortly before departure instead of sitting full all night; it may cost grid, and this quote says how much.")]
        string? priority = null,
        [Description("Where a 'justInTime' hold parks the car before the last stretch is released. Defaults to the installation's configured rest level. Means nothing under 'cheapest'.")]
        double? restSocPercent = null,
        [Description("The charger may not run before this, ISO-8601 with an offset. Optional lower bound.")]
        string? notBefore = null,
        [Description("The charger may not run after this, ISO-8601 with an offset. The deadline applies regardless, so this can only pull the window in.")]
        string? notAfter = null,
        [Description("The most that may be bought over the whole plan, in watt-hours. Zero is a real value and means sun only: the request is met from the roof or not at all, and the rest is reported as shortfall rather than quietly imported.")]
        double? maxGridEnergyWh = null,
        CancellationToken ct = default) =>
        client.PostAsync(
            Endpoints.QuotePlan,
            Requests.ComposeTargeted(
                energyKWh, targetSocPercent, departBy, priority, restSocPercent,
                notBefore, notAfter, maxGridEnergyWh, planId: null),
            ct);
}
