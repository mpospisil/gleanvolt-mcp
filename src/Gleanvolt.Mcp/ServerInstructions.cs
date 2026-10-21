namespace Gleanvolt.Mcp;

/// <summary>
/// What the client is told about this server before it asks for anything — the <c>instructions</c> of
/// the initialize handshake, which a model reads alongside the tool list.
///
/// <para>It exists for one failure that has actually happened. Asked to start a charge against a
/// read-only server, a model finds no tool that starts anything and says, in effect, that it does not
/// know how — which is true and useless. The capability is not missing because the installation cannot
/// do it, but because this server was launched without
/// <see cref="GleanvoltOptions.WritesVariable"/>, and that is a fact only the server knows and only
/// the operator can change. Not advertising a tool that would always refuse is still right; leaving
/// nobody able to say <em>why</em> was not.</para>
/// </summary>
internal static class ServerInstructions
{
    private const string What =
        """
        These tools serve one Gleanvolt installation: a hybrid inverter, a home battery, an EV charger
        and the roof above them, read over the local network. Every figure comes from that one site.

        Every figure is in a base unit, named in its field: power in W (...Watts), energy in Wh (...Wh),
        durations in seconds (...Seconds), distance in metres (...Metres), pressure in Pa, percentages
        0-100. Convert for the person -- 22000 Wh is 22 kWh, 176000 m is 176 km -- and pass amounts
        back in the same base units.
        """;

    /// <summary>
    /// How to pick a mode from what the person actually said. Keyed by intent rather than listing the
    /// modes again: the tool descriptions say what each mode does, and a model choosing between them
    /// needs the question turned the other way round. The one trap worth naming is an amount: only
    /// targeted takes one, and the sun-only cap is what replaced Forecasted's session energy target
    /// (gleanvolt #249).
    /// </summary>
    private const string ChoosingAMode =
        """
        Choosing a charging mode, from what the person wants:

        - An amount (kWh or a battery %) by a time -> targeted: quote it with gleanvolt_quote_plan, then
          gleanvolt_start_targeted. It uses the sun first and buys grid only for what the forecast cannot
          cover. If they want sun only and will accept falling short, pass maxGridEnergyWh=0: the plan
          is then met from the roof or not at all, and the gap is reported rather than bought. A
          departure up to about 36 hours ahead is allowed, so "10 kWh from the sun, no rush" is a
          targeted charge with a far departure and maxGridEnergyWh=0.
        - Free sun, no amount, no deadline -> forecasted. It never deliberately buys grid, keeps the
          home battery on course for 100% by the evening, and may lend from it briefly. It takes NO
          amount: it charges until the car's own charge limit stops it, or the sun runs out. If they
          want to stop at a level, that is the charge limit set in the car, or a targeted charge.
        - Sun only after the home battery is full -> solar, the most conservative choice.
        - Sun first, with a little grid to keep the car moving on thin sun -> solarGrid.
        - As fast as possible, now -> fastNoBattery (grid allowed, home battery held out of it). It can
          stop at an amount, and with fastDepartBy it defers the start so the car is not left full
          overnight.

        If the request does not settle it -- an amount with no time, "cheap" without saying whether a
        shortfall is acceptable -- ask, rather than picking for them.
        """;

    private const string ReadOnly =
        """
        This server is READ-ONLY. The tools that move hardware -- gleanvolt_start,
        gleanvolt_start_targeted, gleanvolt_stop and gleanvolt_set_battery_hold -- are not registered,
        so nothing here can start or stop a charge or arm the battery hold.

        If you are asked to do any of those, do not report that you do not know how, and do not
        improvise with the read tools. Say that this server is running read-only, and that the operator
        can change it by setting GLEANVOLT_MCP_ALLOW_WRITES=true in the server's environment and
        {0}. Then offer what you can still do: gleanvolt_quote_plan prices a targeted
        charge exactly as the real thing would run, writes to nothing, and is worth showing anyway --
        the person can start it themselves from the web UI once they have seen the numbers.
        """;

    private const string Writable =
        """
        This server CAN move hardware: four of its tools write to a charger and an inverter attached to
        a real car and a real house.

        Confirm with the person before calling any of them, and say which mode and why. For a targeted
        charge, quote it first with gleanvolt_quote_plan and show what it says -- the grid it expects to
        buy, and any shortfall -- before committing with gleanvolt_start_targeted and the same
        arguments. Report what a call actually returned rather than that it succeeded: a refused write
        comes back as a normal response with succeeded=false, and an armed battery hold that the
        inverter quietly ignored is visible only in the power read back afterwards.
        """;

    /// <summary>
    /// Who has to restart what, which is not the same sentence in both transports and is the one part
    /// of this text an operator is expected to act on. A stdio server is spawned by its client and
    /// dies with it, so the client is the thing to restart; an HTTP server outlives every client
    /// connected to it, and restarting Home Assistant would achieve nothing at all.
    /// </summary>
    private static string Restart(Transport transport) => transport switch
    {
        Transport.Http => "restarting the server process",
        _ => "restarting the client",
    };

    /// <summary>The instructions for a server launched with, or without, permission to write.</summary>
    internal static string For(bool allowWrites, Transport transport) =>
        allowWrites
            ? $"{What}\n\n{Writable}\n\n{ChoosingAMode}"
            : $"{What}\n\n{string.Format(null, ReadOnly, Restart(transport))}\n\n{ChoosingAMode}";
}
