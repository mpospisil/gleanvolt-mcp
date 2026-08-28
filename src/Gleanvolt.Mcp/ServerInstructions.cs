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
        """;

    private const string ReadOnly =
        """
        This server is READ-ONLY. The tools that move hardware -- gleanvolt_start,
        gleanvolt_start_targeted, gleanvolt_stop and gleanvolt_set_battery_hold -- are not registered,
        so nothing here can start or stop a charge or arm the battery hold.

        If you are asked to do any of those, do not report that you do not know how, and do not
        improvise with the read tools. Say that this server is running read-only, and that the operator
        can change it by setting GLEANVOLT_MCP_ALLOW_WRITES=true in the server's environment and
        restarting the client. Then offer what you can still do: gleanvolt_quote_plan prices a targeted
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

    /// <summary>The instructions for a server launched with, or without, permission to write.</summary>
    internal static string For(bool allowWrites) =>
        $"{What}\n\n{(allowWrites ? Writable : ReadOnly)}";
}
