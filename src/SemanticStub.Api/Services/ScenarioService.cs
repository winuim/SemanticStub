using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

/// <summary>
/// Stores scenario state transitions in memory so YAML-defined stateful flows can advance deterministically across requests.
/// </summary>
public sealed class ScenarioService
{
    private readonly ScenarioStateStore stateStore = new();
    private readonly SemaphoreSlim semaphore = new(1, 1);

    /// <summary>
    /// Returns whether the supplied scenario definition is eligible for the current in-memory state.
    /// </summary>
    /// <param name="scenario">The scenario constraint attached to a response. <see langword="null"/> means the response is always eligible.</param>
    public bool IsMatch(ScenarioDefinition? scenario)
    {
        if (scenario is null)
        {
            return true;
        }

        return stateStore.IsMatch(scenario);
    }

    /// <summary>
    /// Persists the next state for the supplied scenario when the selected response defines an explicit transition.
    /// </summary>
    /// <param name="scenario">The scenario definition attached to the selected response.</param>
    public void Advance(ScenarioDefinition? scenario)
    {
        stateStore.Advance(scenario, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Clears all in-memory scenario progress so subsequent requests start from each scenario's initial state again.
    /// </summary>
    public void Reset()
    {
        ExecuteLocked(() =>
        {
            ResetWithinLock();
            return 0;
        });
    }

    internal void ResetWithinLock()
    {
        stateStore.Clear();
    }

    /// <summary>
    /// Returns the point-in-time snapshot for the supplied scenario.
    /// </summary>
    /// <param name="scenarioName">The scenario name defined in YAML.</param>
    public ScenarioStateSnapshot GetSnapshot(string scenarioName)
    {
        return ExecuteLocked(() => GetSnapshotWithinLock(scenarioName));
    }

    /// <summary>
    /// Returns point-in-time snapshots for the supplied scenarios.
    /// </summary>
    /// <param name="scenarioNames">The scenario names defined in YAML.</param>
    public IReadOnlyDictionary<string, ScenarioStateSnapshot> GetSnapshots(IEnumerable<string> scenarioNames)
    {
        return ExecuteLocked(() =>
        {
            var snapshots = new Dictionary<string, ScenarioStateSnapshot>(StringComparer.Ordinal);

            foreach (var scenarioName in scenarioNames)
            {
                snapshots[scenarioName] = GetSnapshotWithinLock(scenarioName);
            }

            return snapshots;
        });
    }

    /// <summary>
    /// Resets the supplied scenario back to its initial state.
    /// </summary>
    /// <param name="scenarioName">The scenario name defined in YAML.</param>
    public void ResetScenario(string scenarioName)
    {
        ExecuteLocked(() =>
        {
            ResetScenarioWithinLock(scenarioName, DateTimeOffset.UtcNow);
            return 0;
        });
    }

    /// <summary>
    /// Resets the supplied scenarios back to their initial state.
    /// </summary>
    /// <param name="scenarioNames">The scenario names defined in YAML.</param>
    public void ResetScenarios(IEnumerable<string> scenarioNames)
    {
        ExecuteLocked(() =>
        {
            ResetScenariosWithinLock(scenarioNames, DateTimeOffset.UtcNow);
            return 0;
        });
    }

    internal ScenarioStateSnapshot GetSnapshotWithinLock(string scenarioName)
    {
        return stateStore.GetSnapshot(scenarioName);
    }

    internal void ResetScenarioWithinLock(string scenarioName, DateTimeOffset timestamp)
    {
        stateStore.ResetScenario(scenarioName, timestamp);
    }

    internal void ResetScenariosWithinLock(IEnumerable<string> scenarioNames, DateTimeOffset timestamp)
    {
        stateStore.ResetScenarios(scenarioNames, timestamp);
    }

    /// <summary>
    /// Executes scenario-sensitive selection and transition logic under one lock so state checks and advances stay atomic across concurrent requests.
    /// </summary>
    /// <param name="action">The scenario-aware operation to run atomically.</param>
    public T ExecuteLocked<T>(Func<T> action)
    {
        semaphore.Wait();
        try
        {
            return action();
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Executes scenario-sensitive selection and transition logic under one asynchronous lock so state checks and advances stay atomic across concurrent async requests.
    /// </summary>
    /// <param name="action">The scenario-aware async operation to run atomically.</param>
    public async Task<T> ExecuteLockedAsync<T>(Func<Task<T>> action)
    {
        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }
}

/// <summary>
/// Describes a scenario's current runtime state and when it last changed.
/// </summary>
public sealed record ScenarioStateSnapshot(string State, DateTimeOffset? LastUpdatedTimestamp);
