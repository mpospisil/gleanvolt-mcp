using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Gleanvolt.Mcp.Tools;

/// <summary>
/// The four tools that move hardware. Registered only when GLEANVOLT_MCP_ALLOW_WRITES is true — a
/// read-only server does not advertise them at all, which is a better answer than advertising a tool
/// that always refuses.
/// </summary>
[McpServerToolType]
internal static class ControlTools
{
    [McpServerTool(Name = "gleanvolt_start", Title = "Start charging in a mode", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("""
        Start controlled charging in a mode. This MOVES REAL HARDWARE — confirm with the person before
        calling it, and say which mode and why.

        Modes: 'solar' follows live surplus once the home battery is full. 'forecasted' lets today's
        forecast decide how much of the sun the car may have, so the home battery still reaches 100% by
        its evening deadline. 'fastNoBattery' charges flat out from PV and grid with the home battery
        held out of it, and ends itself when the car is full.

        For a targeted charge use gleanvolt_quote_plan then gleanvolt_start_targeted instead — this
        tool refuses 'targeted'. To stop, use gleanvolt_stop rather than passing 'off'.
        """)]
    internal static async Task<string> Start(
        GleanvoltClient client,
        [Description("One of: solar, forecasted, fastNoBattery.")] string mode,
        [Description("For 'fastNoBattery' only: what to aim at — 'full' (charge until the car itself stops, the default), 'energy', or 'soc'.")]
        string? fastBasis = null,
        [Description("For fastBasis 'energy': the energy to deliver, measured at the charger.")]
        double? fastEnergyKWh = null,
        [Description("For fastBasis 'soc': the state of charge to stop at. Converted to energy once, when the mode starts, and never re-derived from a later reading.")]
        double? fastTargetSocPercent = null,
        [Description("For 'fastNoBattery': when the car has to be ready, ISO-8601 with an offset. Defers the charge to the latest moment that still finishes in time, which keeps the pack off a high state of charge overnight. Needs an amount to work back from, so it is refused with basis 'full'.")]
        string? fastDepartBy = null,
        CancellationToken ct = default)
    {
        if (string.Equals(mode, "targeted", StringComparison.OrdinalIgnoreCase))
        {
            return Refusal("A targeted charge is quoted before it is committed. Call gleanvolt_quote_plan, show the person the numbers, then call gleanvolt_start_targeted.");
        }

        if (string.Equals(mode, "off", StringComparison.OrdinalIgnoreCase))
        {
            return Refusal("'off' is not a mode to start. Call gleanvolt_stop.");
        }

        var fast = fastBasis is null && fastEnergyKWh is null && fastTargetSocPercent is null && fastDepartBy is null
            ? null
            : new Requests.FastLimit
            {
                Basis = fastBasis ?? "full",
                EnergyKWh = fastEnergyKWh,
                TargetSocPercent = fastTargetSocPercent,
                DepartBy = fastDepartBy,
            };

        return await client.PostAsync(Endpoints.Start, new Requests.Start { Mode = mode, Fast = fast }, ct);
    }

    [McpServerTool(Name = "gleanvolt_start_targeted", Title = "Commit to a quoted plan", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("""
        Start a targeted charge — deliver a stated amount by a stated time. This MOVES REAL HARDWARE.

        Call gleanvolt_quote_plan FIRST with the same arguments and show the person what it says,
        including any shortfall and any grid it expects to buy. Then call this with those arguments
        unchanged, plus the planId the quote returned: the response will say whether the forecast has
        moved since the quote. The planId is advisory — it never blocks a start.
        """)]
    internal static Task<string> StartTargeted(
        GleanvoltClient client,
        [Description("When the energy has to be in the car. ISO-8601 WITH an offset — this is a local-time promise.")]
        string departBy,
        [Description("The energy to deliver, measured at the charger. Omit when asking in state of charge.")]
        double? energyKWh = null,
        [Description("The state of charge to reach, 0-100. Omit when asking in kilowatt-hours.")]
        double? targetSocPercent = null,
        [Description("'cheapest' (default) or 'justInTime'. Must match what was quoted.")]
        string? priority = null,
        [Description("Where a 'justInTime' hold parks the car before the last stretch is released.")]
        double? restSocPercent = null,
        [Description("The charger may not run before this, ISO-8601 with an offset.")]
        string? notBefore = null,
        [Description("The charger may not run after this, ISO-8601 with an offset.")]
        string? notAfter = null,
        [Description("The most that may be bought over the whole plan, in watt-hours. Zero means sun only.")]
        double? maxGridEnergyWh = null,
        [Description("The planId from the quote this is committing to. Advisory: the response reports whether the forecast has moved since, and a start never fails because of it.")]
        string? planId = null,
        CancellationToken ct = default) =>
        client.PostAsync(
            Endpoints.StartTargeted,
            Requests.ComposeTargeted(
                energyKWh, targetSocPercent, departBy, priority, restSocPercent,
                notBefore, notAfter, maxGridEnergyWh, planId),
            ct);

    [McpServerTool(Name = "gleanvolt_stop", Title = "Stop controlled charging", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("""
        Stop controlled charging: return the mode to 'off' and leave the charger's setpoint where it
        is. This MOVES REAL HARDWARE. Safe to call when nothing is running.
        """)]
    internal static Task<string> Stop(GleanvoltClient client, CancellationToken ct) =>
        client.PostAsync(Endpoints.Stop, new { }, ct);

    [McpServerTool(Name = "gleanvolt_set_battery_hold", Title = "Arm or release the battery hold", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("""
        Arm or release the home battery's discharge hold. Armed, the pack stops serving house load, so
        the car charges from PV and grid while the pack still charges from surplus. Orthogonal to the
        charge mode: either can be on without the other. This MOVES REAL HARDWARE.

        The result includes a 'verification' block read back from the inverter a moment after the
        write. Report what that block says, NOT merely that the call succeeded: on some installations
        the hold is accepted and then ignored, and a battery still discharging while the hold is armed
        is the observable symptom. Judge by the read-back power, never by the acknowledgement.
        """)]
    internal static async Task<string> SetBatteryHold(
        GleanvoltClient client,
        [Description("True stops the home battery serving house load; false releases it.")] bool hold,
        CancellationToken ct = default)
    {
        var written = await client.PutAsync(Endpoints.BatteryHold, new Requests.BatteryHold { Hold = hold }, ct);

        // The read-back is the whole point of this tool being more than a passthrough: a 200 here means
        // the register was written, not that the inverter honoured it.
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        var status = await client.GetAsync(Endpoints.Status, ct);

        var note = hold
            ? "The hold was requested. Check batteryPowerW in the status below: a negative value (discharging) while the hold is armed means the inverter accepted the write and ignored it. Say so plainly rather than reporting success."
            : "The hold was released. The battery may resume serving house load.";

        // Concatenated rather than interpolated: the closing braces of a nested JSON object and those
        // of a raw-string hole are the same character, and the compiler is right to object.
        return "{\"written\":" + written
            + ",\"verification\":{\"note\":" + JsonSerializer.Serialize(note)
            + ",\"statusAfter\":" + status + "}}";
    }

    private static string Refusal(string message) =>
        JsonSerializer.Serialize(new { error = new { status = 0, reason = "Refused by this server.", detail = message } });
}
