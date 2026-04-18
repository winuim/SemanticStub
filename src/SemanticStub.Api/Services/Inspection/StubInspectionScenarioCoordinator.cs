using SemanticStub.Api.Inspection;
using SemanticStub.Api.Infrastructure.Yaml;

namespace SemanticStub.Api.Services;

internal sealed class StubInspectionScenarioCoordinator
{
    private readonly StubDefinitionState _state;
    private readonly ScenarioService _scenarioService;

    public StubInspectionScenarioCoordinator(StubDefinitionState state, ScenarioService scenarioService)
    {
        _state = state;
        _scenarioService = scenarioService;
    }

    public IReadOnlyList<ScenarioStateInfo> GetScenarioStates()
    {
        return _scenarioService.ExecuteLocked(() =>
        {
            var scenarioNames = GetCurrentScenarioNames();

            return scenarioNames
                .Select(CreateScenarioState)
                .ToList();
        });
    }

    public void ResetScenarioStates()
    {
        _scenarioService.ExecuteLocked(() =>
        {
            _scenarioService.ResetScenariosWithinLock(GetCurrentScenarioNames(), DateTimeOffset.UtcNow);
            return 0;
        });
    }

    public bool ResetScenarioState(string scenarioName)
    {
        return _scenarioService.ExecuteLocked(() =>
        {
            var scenarioNames = GetCurrentScenarioNames();

            if (!scenarioNames.Contains(scenarioName, StringComparer.Ordinal))
            {
                return false;
            }

            _scenarioService.ResetScenarioWithinLock(scenarioName, DateTimeOffset.UtcNow);
            return true;
        });
    }

    private IReadOnlyList<string> GetCurrentScenarioNames()
    {
        return StubInspectionDocumentProjector.GetScenarioNames(_state.GetCurrentDocument());
    }

    private ScenarioStateInfo CreateScenarioState(string scenarioName)
    {
        var snapshot = _scenarioService.GetSnapshotWithinLock(scenarioName);

        return new ScenarioStateInfo
        {
            Name = scenarioName,
            CurrentState = snapshot.State,
            LastUpdatedTimestamp = snapshot.LastUpdatedTimestamp,
        };
    }
}
