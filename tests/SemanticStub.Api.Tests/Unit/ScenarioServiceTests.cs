using SemanticStub.Api.Services;
using SemanticStub.Api.Models;
using System.Threading;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class ScenarioServiceTests
{
    [Fact]
    public async Task ExecuteLocked_SerializesConcurrentAccess()
    {
        var service = new ScenarioService();
        var concurrentExecutions = 0;
        var maxConcurrentExecutions = 0;

        Task RunLockedSectionAsync()
        {
            return Task.Run(() => service.ExecuteLocked(() =>
            {
                var current = Interlocked.Increment(ref concurrentExecutions);
                UpdateMax(ref maxConcurrentExecutions, current);
                Thread.Sleep(100);
                Interlocked.Decrement(ref concurrentExecutions);
                return 0;
            }));
        }

        await Task.WhenAll(RunLockedSectionAsync(), RunLockedSectionAsync());

        Assert.Equal(1, maxConcurrentExecutions);
    }

    [Fact]
    public void Reset_ClearsAdvancedScenarioState()
    {
        var service = new ScenarioService();
        var scenario = new ScenarioDefinition
        {
            Name = "checkout-flow",
            State = "initial",
            Next = "confirmed"
        };

        service.Advance(scenario);

        Assert.False(service.IsMatch(scenario));

        service.Reset();

        Assert.True(service.IsMatch(scenario));
    }

    [Fact]
    public void GetSnapshot_ReturnsInitialStateWithNullTimestamp_WhenScenarioHasNotAdvanced()
    {
        var service = new ScenarioService();

        var snapshot = service.GetSnapshot("checkout-flow");

        Assert.Equal("initial", snapshot.State);
        Assert.Null(snapshot.LastUpdatedTimestamp);
    }

    [Fact]
    public void GetSnapshots_ReturnsCurrentSnapshotForEachRequestedScenario()
    {
        var service = new ScenarioService();
        service.Advance(new ScenarioDefinition { Name = "checkout-flow", State = "initial", Next = "confirmed" });

        var snapshots = service.GetSnapshots(["checkout-flow", "payment-flow"]);

        Assert.Equal(2, snapshots.Count);
        Assert.Equal("confirmed", snapshots["checkout-flow"].State);
        Assert.NotNull(snapshots["checkout-flow"].LastUpdatedTimestamp);
        Assert.Equal("initial", snapshots["payment-flow"].State);
        Assert.Null(snapshots["payment-flow"].LastUpdatedTimestamp);
    }

    [Fact]
    public void Advance_StoresCurrentStateTimestamp()
    {
        var service = new ScenarioService();
        var scenario = new ScenarioDefinition
        {
            Name = "checkout-flow",
            State = "initial",
            Next = "confirmed"
        };
        var before = DateTimeOffset.UtcNow;

        service.Advance(scenario);

        var after = DateTimeOffset.UtcNow;
        var snapshot = service.GetSnapshot("checkout-flow");

        Assert.Equal("confirmed", snapshot.State);
        Assert.NotNull(snapshot.LastUpdatedTimestamp);
        Assert.True(snapshot.LastUpdatedTimestamp >= before);
        Assert.True(snapshot.LastUpdatedTimestamp <= after);
    }

    [Fact]
    public void ResetScenario_RestoresInitialStateWithTimestamp()
    {
        var service = new ScenarioService();
        var scenario = new ScenarioDefinition
        {
            Name = "checkout-flow",
            State = "initial",
            Next = "confirmed"
        };

        service.Advance(scenario);

        service.ResetScenario("checkout-flow");

        var snapshot = service.GetSnapshot("checkout-flow");
        Assert.Equal("initial", snapshot.State);
        Assert.NotNull(snapshot.LastUpdatedTimestamp);
    }

    [Fact]
    public void ResetScenarios_ResetsOnlyTheSuppliedScenarioSet()
    {
        var service = new ScenarioService();
        service.Advance(new ScenarioDefinition { Name = "checkout-flow", State = "initial", Next = "confirmed" });
        service.Advance(new ScenarioDefinition { Name = "payment-flow", State = "initial", Next = "authorized" });

        service.ResetScenarios(["checkout-flow"]);

        var checkoutSnapshot = service.GetSnapshot("checkout-flow");
        var paymentSnapshot = service.GetSnapshot("payment-flow");

        Assert.Equal("initial", checkoutSnapshot.State);
        Assert.NotNull(checkoutSnapshot.LastUpdatedTimestamp);
        Assert.Equal("initial", paymentSnapshot.State);
        Assert.Null(paymentSnapshot.LastUpdatedTimestamp);
    }

    [Fact]
    public void ResetWithinLock_ClearsAdvancedScenarioStateWithoutDeadlock()
    {
        var service = new ScenarioService();
        var scenario = new ScenarioDefinition
        {
            Name = "checkout-flow",
            State = "initial",
            Next = "confirmed"
        };

        service.Advance(scenario);

        service.ExecuteLocked(() =>
        {
            service.ResetWithinLock();
        });

        Assert.True(service.IsMatch(scenario));
    }

    private static void UpdateMax(ref int target, int candidate)
    {
        while (true)
        {
            var snapshot = target;

            if (candidate <= snapshot)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref target, candidate, snapshot) == snapshot)
            {
                return;
            }
        }
    }
}
