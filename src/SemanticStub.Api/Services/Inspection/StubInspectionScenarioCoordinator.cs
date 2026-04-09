using SemanticStub.Api.Inspection;
using SemanticStub.Api.Infrastructure.Yaml;

namespace SemanticStub.Api.Services;

internal sealed class StubInspectionScenarioCoordinator
{
    private readonly StubDefinitionState state;
    private readonly ScenarioService scenarioService;

    public StubInspectionScenarioCoordinator(StubDefinitionState state, ScenarioService scenarioService)
    {
        this.state = state;
        this.scenarioService = scenarioService;
    }

    public IReadOnlyList<ScenarioStateInfo> GetScenarioStates()
    {
        return scenarioService.ExecuteLocked(() =>
        {
            var scenarioNames = GetCurrentScenarioNames();

            return scenarioNames
                .Select(CreateScenarioState)
                .ToList();
        });
    }

    public void ResetScenarioStates()
    {
        scenarioService.ExecuteLocked(() =>
        {
            scenarioService.ResetScenariosWithinLock(GetCurrentScenarioNames(), DateTimeOffset.UtcNow);
            return 0;
        });
    }

    public bool ResetScenarioState(string scenarioName)
    {
        return scenarioService.ExecuteLocked(() =>
        {
            var scenarioNames = GetCurrentScenarioNames();

            if (!scenarioNames.Contains(scenarioName, StringComparer.Ordinal))
            {
                return false;
            }

            scenarioService.ResetScenarioWithinLock(scenarioName, DateTimeOffset.UtcNow);
            return true;
        });
    }

    private IReadOnlyList<string> GetCurrentScenarioNames()
    {
        return StubInspectionDocumentProjector.GetScenarioNames(state.GetCurrentDocument());
    }

    private ScenarioStateInfo CreateScenarioState(string scenarioName)
    {
        var snapshot = scenarioService.GetSnapshotWithinLock(scenarioName);

        return new ScenarioStateInfo
        {
            Name = scenarioName,
            CurrentState = snapshot.State,
            LastUpdatedTimestamp = snapshot.LastUpdatedTimestamp,
        };
    }
}
