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
            return 0;
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
