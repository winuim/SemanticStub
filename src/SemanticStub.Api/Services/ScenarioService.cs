using SemanticStub.Api.Models;
using System.Collections.Concurrent;

namespace SemanticStub.Api.Services;

/// <summary>
/// Stores scenario state transitions in memory so YAML-defined stateful flows can advance deterministically across requests.
/// </summary>
public sealed class ScenarioService
{
    private const string InitialState = "initial";
    private readonly ConcurrentDictionary<string, string> currentStates = new(StringComparer.Ordinal);
    private readonly object syncRoot = new();

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

        var currentState = currentStates.GetValueOrDefault(scenario.Name, InitialState);

        return string.Equals(currentState, scenario.State, StringComparison.Ordinal);
    }

    /// <summary>
    /// Persists the next state for the supplied scenario when the selected response defines an explicit transition.
    /// </summary>
    /// <param name="scenario">The scenario definition attached to the selected response.</param>
    public void Advance(ScenarioDefinition? scenario)
    {
        if (scenario is null || string.IsNullOrWhiteSpace(scenario.Next))
        {
            return;
        }

        currentStates[scenario.Name] = scenario.Next;
    }

    /// <summary>
    /// Executes scenario-sensitive selection and transition logic under one lock so state checks and advances stay atomic across concurrent requests.
    /// </summary>
    /// <param name="action">The scenario-aware operation to run atomically.</param>
    public T ExecuteLocked<T>(Func<T> action)
    {
        lock (syncRoot)
        {
            return action();
        }
    }
}
