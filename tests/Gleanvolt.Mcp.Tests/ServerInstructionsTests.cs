namespace Gleanvolt.Mcp.Tests;

/// <summary>
/// The instructions are the read-only server's only voice. Asked to start a charge it has no tool for,
/// a model can either say "I don't know how" — which is what happened, and is useless — or say the
/// server is read-only and name the switch. These tests pin the second.
/// </summary>
public sealed class ServerInstructionsTests
{
    [Fact]
    public void A_read_only_server_names_the_switch_and_who_can_throw_it()
    {
        foreach (var transport in new[] { Transport.Stdio, Transport.Http })
        {
            var instructions = ServerInstructions.For(allowWrites: false, transport);

            // The variable by name: an operator reading this over the model's shoulder has to be able
            // to act on it without going to the README first.
            Assert.Contains(GleanvoltOptions.WritesVariable, instructions, StringComparison.Ordinal);
            Assert.Contains("READ-ONLY", instructions, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The advice has to be actionable in the transport it is given in. A stdio server is respawned by
    /// its client, so restarting the client is what picks the variable up; an HTTP server outlives
    /// every client attached to it, and telling someone to restart Home Assistant would send them off
    /// to do a thing that changes nothing.
    /// </summary>
    [Fact]
    public void A_read_only_server_names_the_restart_that_would_actually_help()
    {
        Assert.Contains(
            "restarting the client",
            ServerInstructions.For(allowWrites: false, Transport.Stdio),
            StringComparison.Ordinal);

        var http = ServerInstructions.For(allowWrites: false, Transport.Http);

        Assert.Contains("restarting the server process", http, StringComparison.Ordinal);
        Assert.DoesNotContain("restarting the client", http, StringComparison.Ordinal);
    }

    [Fact]
    public void A_read_only_server_names_the_tools_it_is_missing()
    {
        var instructions = ServerInstructions.For(allowWrites: false, Transport.Stdio);

        // Named rather than described, so the answer is specific about what cannot be done here.
        Assert.Contains("gleanvolt_start", instructions, StringComparison.Ordinal);
        Assert.Contains("gleanvolt_stop", instructions, StringComparison.Ordinal);
        Assert.Contains("gleanvolt_set_battery_hold", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void A_read_only_server_offers_the_quote_it_can_still_give()
    {
        // The failure is not only "no", it is "no, and nothing instead". A priced plan is genuinely
        // useful even when this server cannot start it.
        Assert.Contains("gleanvolt_quote_plan", ServerInstructions.For(allowWrites: false, Transport.Stdio), StringComparison.Ordinal);
    }

    [Fact]
    public void A_writable_server_does_not_tell_anyone_to_enable_anything()
    {
        var instructions = ServerInstructions.For(allowWrites: true, Transport.Stdio);

        Assert.DoesNotContain(GleanvoltOptions.WritesVariable, instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("READ-ONLY", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void A_writable_server_says_what_it_can_break_and_how_to_report_it()
    {
        var instructions = ServerInstructions.For(allowWrites: true, Transport.Stdio);

        Assert.Contains("Confirm with the person", instructions, StringComparison.Ordinal);
        Assert.Contains("gleanvolt_quote_plan", instructions, StringComparison.Ordinal);

        // The two ways a call can look successful and not be, both of which the tools already warn
        // about individually -- said once more where the model reads before it starts.
        Assert.Contains("succeeded=false", instructions, StringComparison.Ordinal);
        Assert.Contains("battery hold", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Both_say_which_installation_this_is()
    {
        foreach (var instructions in new[]
                 {
                     ServerInstructions.For(true, Transport.Stdio),
                     ServerInstructions.For(false, Transport.Stdio),
                     ServerInstructions.For(true, Transport.Http),
                     ServerInstructions.For(false, Transport.Http),
                 })
        {
            Assert.Contains("one Gleanvolt installation", instructions, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The server picks a mode for the person, so the instructions turn the question round: from what
    /// they want to the mode that does it. The trap is an amount. Forecasted used to take a session
    /// ceiling and no longer does (gleanvolt #249); a model that still offers it one, or that does not
    /// know targeted can be held to the sun, picks the wrong mode.
    /// </summary>
    [Fact]
    public void Both_say_how_to_choose_a_mode_and_that_only_targeted_takes_an_amount()
    {
        foreach (var allowWrites in new[] { true, false })
        {
            var instructions = ServerInstructions.For(allowWrites, Transport.Stdio);

            foreach (var mode in new[] { "targeted", "forecasted", "solar", "solarGrid", "fastNoBattery" })
            {
                Assert.Contains($"-> {mode}", instructions, StringComparison.Ordinal);
            }

            // Whitespace collapsed: the raw string wraps where it likes, the sentence is what matters.
            var flat = System.Text.RegularExpressions.Regex.Replace(instructions, @"\s+", " ");
            Assert.Contains("It takes NO amount", flat, StringComparison.Ordinal);
            Assert.Contains("maxGridEnergyWh=0", instructions, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The API carries base units only (gleanvolt #263), and the JSON reaches the model as it is. Without
    /// being told, a model reads "solarWh": 22000 as 22000 kWh or asks for 22 Wh when it means 22 kWh.
    /// </summary>
    [Fact]
    public void Both_say_the_figures_are_in_base_units()
    {
        foreach (var allowWrites in new[] { true, false })
        {
            var flat = System.Text.RegularExpressions.Regex.Replace(
                ServerInstructions.For(allowWrites, Transport.Stdio), @"\s+", " ");

            Assert.Contains("energy in Wh", flat, StringComparison.Ordinal);
            Assert.Contains("22000 Wh is 22 kWh", flat, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_read_only_server_names_the_tariff_tools_it_does_not_have()
    {
        var instructions = ServerInstructions.For(allowWrites: false, Transport.Http);

        Assert.Contains("gleanvolt_set_low_tariff_only", instructions, StringComparison.Ordinal);
        Assert.Contains("gleanvolt_set_tariff", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Low_tariff_charging_is_steered_to_the_option_and_to_checking_a_tariff_exists()
    {
        foreach (var allowWrites in new[] { true, false })
        {
            var instructions = ServerInstructions.For(allowWrites, Transport.Http);

            var flat = System.Text.RegularExpressions.Regex.Replace(instructions, @"\s+", " ");

            Assert.Contains("fastNoBattery with low tariff only set", flat, StringComparison.Ordinal);
            Assert.Contains("Check gleanvolt_tariff first", flat, StringComparison.Ordinal);
        }
    }
}
