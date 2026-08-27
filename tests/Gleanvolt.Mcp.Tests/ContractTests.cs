using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gleanvolt.Mcp.Tests;

/// <summary>
/// This repository owns no schema of its own — the installation's OpenAPI document does — so what
/// there is to test is whether the two still agree. These are the tests that turn "the document is the
/// deliverable" into something a build can enforce from the consuming side.
/// </summary>
public sealed class ContractTests
{
    /// <summary>
    /// Every route a tool calls is a route the API actually publishes, under the verb it is called by.
    /// A renamed endpoint fails here rather than at the first 404 in front of a model.
    /// </summary>
    [Theory]
    [MemberData(nameof(Routes))]
    public void EveryRouteExistsInTheContract(string method, string template)
    {
        Assert.True(
            Contract.Paths.TryGetProperty(template, out var operations),
            $"{template} is not in contract/openapi.json. The API moved, or the constant is wrong.");

        Assert.True(
            operations.TryGetProperty(method, out _),
            $"{template} exists but has no {method.ToUpperInvariant()} operation.");
    }

    public static TheoryData<string, string> Routes()
    {
        var data = new TheoryData<string, string>();

        foreach (var (method, template) in Endpoints.All)
        {
            data.Add(method, template);
        }

        return data;
    }

    /// <summary>
    /// Every field these request records send is a field the schema declares. This is the failure the
    /// checked-in document exists to catch: a property renamed upstream leaves a tool quietly sending
    /// something the API ignores, and a charge that silently loses its grid cap is worse than one that
    /// is refused.
    /// </summary>
    [Theory]
    [InlineData(typeof(Requests.Targeted), "TargetedChargeRequestBody")]
    [InlineData(typeof(Requests.Editable), "EditablePlanBody")]
    [InlineData(typeof(Requests.FastLimit), "FastChargeLimitBody")]
    [InlineData(typeof(Requests.Start), "StartChargingRequest")]
    [InlineData(typeof(Requests.BatteryHold), "BatteryHoldRequest")]
    public void EveryRequestFieldIsInTheSchema(Type record, string schemaName)
    {
        var declared = Contract.PropertiesOf(schemaName);

        foreach (var property in record.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;

            Assert.NotNull(name);
            Assert.True(
                declared.Contains(name),
                $"{record.Name}.{property.Name} serialises as '{name}', which {schemaName} does not declare. "
                + $"It accepts: {string.Join(", ", declared.Order(StringComparer.Ordinal))}.");
        }
    }

    /// <summary>
    /// The reverse direction, as a report rather than a failure for most of it: the API may publish
    /// more than this server chooses to expose — that is curation, not drift — but a required field
    /// that no record sends would be a tool that cannot succeed.
    /// </summary>
    [Theory]
    [InlineData(typeof(Requests.Targeted), "TargetedChargeRequestBody")]
    [InlineData(typeof(Requests.Start), "StartChargingRequest")]
    [InlineData(typeof(Requests.BatteryHold), "BatteryHoldRequest")]
    public void EveryRequiredFieldIsSent(Type record, string schemaName)
    {
        var schema = Contract.Schemas.GetProperty(schemaName);

        if (!schema.TryGetProperty("required", out var required))
        {
            return;
        }

        var sent = record
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var field in required.EnumerateArray().Select(element => element.GetString()!))
        {
            Assert.True(sent.Contains(field), $"{schemaName} requires '{field}', which {record.Name} never sends.");
        }
    }

    /// <summary>
    /// The enum values the tool descriptions name are the ones the API accepts. These reach the model
    /// as prose rather than as a schema enum — flat string parameters are far easier for a model to
    /// fill in correctly than a nested object — so nothing but this test stops the prose drifting.
    /// </summary>
    [Theory]
    [InlineData("ChargeControlMode", "off", "solar", "forecasted", "fastNoBattery", "targeted")]
    [InlineData("TargetedChargePriority", "cheapest", "justInTime")]
    public void TheEnumValuesInDescriptionsAreStillCurrent(string schemaName, params string[] expected)
    {
        var actual = Contract.Schemas
            .GetProperty(schemaName)
            .GetProperty("enum")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
