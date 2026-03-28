using SemanticStub.Api.Services;
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
