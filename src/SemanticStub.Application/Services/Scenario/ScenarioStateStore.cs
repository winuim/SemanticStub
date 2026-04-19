using SemanticStub.Application.Models;
using System.Collections.Concurrent;

namespace SemanticStub.Application.Services;

/// <summary>
/// Stores in-memory scenario state snapshots and applies the runtime transition rules used by <see cref="ScenarioService"/>.
/// </summary>
internal sealed class ScenarioStateStore
{
    private const string InitialState = "initial";
    private readonly ConcurrentDictionary<string, ScenarioStateSnapshot> _currentStates = new(StringComparer.Ordinal);

    public bool IsMatch(ScenarioDefinition scenario)
    {
        var currentState = GetSnapshot(scenario.Name).State;
        return string.Equals(currentState, scenario.State, StringComparison.Ordinal);
    }

    public void Advance(ScenarioDefinition? scenario, DateTimeOffset timestamp)
    {
        if (scenario is null || string.IsNullOrWhiteSpace(scenario.Next))
        {
            return;
        }

        _currentStates[scenario.Name] = CreateSnapshot(scenario.Next, timestamp);
    }

    public ScenarioStateSnapshot GetSnapshot(string scenarioName)
    {
        return _currentStates.TryGetValue(scenarioName, out var snapshot)
            ? snapshot
            : CreateSnapshot(InitialState, timestamp: null);
    }

    public void ResetScenario(string scenarioName, DateTimeOffset timestamp)
    {
        _currentStates[scenarioName] = CreateSnapshot(InitialState, timestamp);
    }

    public void ResetScenarios(IEnumerable<string> scenarioNames, DateTimeOffset timestamp)
    {
        var scenarioNameSet = new HashSet<string>(scenarioNames, StringComparer.Ordinal);

        _currentStates.Clear();

        foreach (var scenarioName in scenarioNameSet)
        {
            _currentStates[scenarioName] = CreateSnapshot(InitialState, timestamp);
        }
    }

    public void Clear()
    {
        _currentStates.Clear();
    }

    private static ScenarioStateSnapshot CreateSnapshot(string state, DateTimeOffset? timestamp)
    {
        return new ScenarioStateSnapshot(state, timestamp);
    }
}
