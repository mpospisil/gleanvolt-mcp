using System.Text.Json;

namespace Gleanvolt.Mcp.Tests;

/// <summary>What the targeted tools send, for the limits that are easy to get subtly wrong.</summary>
public class RequestsTests
{
    private static JsonElement Body(double? maxGridEnergyWh = null, bool? lowTariffOnly = null) =>
        JsonSerializer.SerializeToElement(Requests.ComposeTargeted(
            energyWh: 22_000, targetSocPercent: null, departBy: "2026-11-13T07:00:00+01:00",
            priority: null, restSocPercent: null, notBefore: null, notAfter: null,
            maxGridEnergyWh, lowTariffOnly, planId: null));

    [Fact]
    public void LowTariffOnly_IsSentUnderEditable_WhenAskedFor()
    {
        // gleanvolt #382: the controller waits through high tariff and plans only on low-tariff hours.
        Assert.True(Body(lowTariffOnly: true).GetProperty("editable").GetProperty("lowTariffOnly").GetBoolean());
    }

    [Fact]
    public void LowTariffOnly_False_OrAbsent_SendsNoLimitsAtAll()
    {
        // False is the controller's default; sending it would turn a request that narrows nothing into
        // one with an editable block.
        Assert.False(Body(lowTariffOnly: false).TryGetProperty("editable", out _));
        Assert.False(Body().TryGetProperty("editable", out _));
    }

    [Fact]
    public void SunOnly_StillSendsAZeroCap()
    {
        Assert.Equal(0, Body(maxGridEnergyWh: 0).GetProperty("editable").GetProperty("maxGridEnergyWh").GetDouble());
    }
}
