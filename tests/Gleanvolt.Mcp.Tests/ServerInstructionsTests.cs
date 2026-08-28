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
        var instructions = ServerInstructions.For(allowWrites: false);

        // The variable by name: an operator reading this over the model's shoulder has to be able to
        // act on it without going to the README first.
        Assert.Contains(GleanvoltOptions.WritesVariable, instructions, StringComparison.Ordinal);
        Assert.Contains("READ-ONLY", instructions, StringComparison.Ordinal);
        Assert.Contains("restarting the client", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void A_read_only_server_names_the_tools_it_is_missing()
    {
        var instructions = ServerInstructions.For(allowWrites: false);

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
        Assert.Contains("gleanvolt_quote_plan", ServerInstructions.For(allowWrites: false), StringComparison.Ordinal);
    }

    [Fact]
    public void A_writable_server_does_not_tell_anyone_to_enable_anything()
    {
        var instructions = ServerInstructions.For(allowWrites: true);

        Assert.DoesNotContain(GleanvoltOptions.WritesVariable, instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("READ-ONLY", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void A_writable_server_says_what_it_can_break_and_how_to_report_it()
    {
        var instructions = ServerInstructions.For(allowWrites: true);

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
        foreach (var instructions in new[] { ServerInstructions.For(true), ServerInstructions.For(false) })
        {
            Assert.Contains("one Gleanvolt installation", instructions, StringComparison.Ordinal);
        }
    }
}
