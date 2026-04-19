using SemanticStub.Application.Models;
using SemanticStub.Application.Services;
using Xunit;

namespace SemanticStub.Application.Tests.Unit;

public sealed class ScenarioStateStoreTests
{
    [Fact]
    public void GetSnapshot_ReturnsInitialStateWithNullTimestamp_WhenScenarioHasNotAdvanced()
    {
        var store = new ScenarioStateStore();

        var snapshot = store.GetSnapshot("checkout-flow");

        Assert.Equal("initial", snapshot.State);
        Assert.Null(snapshot.LastUpdatedTimestamp);
    }

    [Fact]
    public void Advance_StoresNextStateAndTimestamp()
    {
        var store = new ScenarioStateStore();
        var scenario = new ScenarioDefinition
        {
            Name = "checkout-flow",
            State = "initial",
            Next = "confirmed"
        };
        var timestamp = DateTimeOffset.UtcNow;

        store.Advance(scenario, timestamp);

        var snapshot = store.GetSnapshot("checkout-flow");
        Assert.Equal("confirmed", snapshot.State);
        Assert.Equal(timestamp, snapshot.LastUpdatedTimestamp);
    }

    [Fact]
    public void ResetScenarios_ClearsUnknownStatesAndDeduplicatesNames()
    {
        var store = new ScenarioStateStore();
        var timestamp = DateTimeOffset.UtcNow;

        store.Advance(new ScenarioDefinition { Name = "checkout-flow", State = "initial", Next = "confirmed" }, timestamp);
        store.Advance(new ScenarioDefinition { Name = "payment-flow", State = "initial", Next = "authorized" }, timestamp);

        store.ResetScenarios(["checkout-flow", "checkout-flow"], timestamp);

        var checkoutSnapshot = store.GetSnapshot("checkout-flow");
        var paymentSnapshot = store.GetSnapshot("payment-flow");

        Assert.Equal("initial", checkoutSnapshot.State);
        Assert.Equal(timestamp, checkoutSnapshot.LastUpdatedTimestamp);
        Assert.Equal("initial", paymentSnapshot.State);
        Assert.Null(paymentSnapshot.LastUpdatedTimestamp);
    }
}
