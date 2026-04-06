using SemanticStub.Api.Inspection;

namespace SemanticStub.Api.Services;

/// <summary>
/// Provides read-only runtime inspection of the active stub configuration.
/// </summary>
public interface IStubInspectionService
{
    /// <summary>
    /// Returns a point-in-time snapshot of the active configuration metadata.
    /// </summary>
    StubConfigSnapshot GetConfigSnapshot();

    /// <summary>
    /// Returns the list of all routes (path + method combinations) currently defined.
    /// </summary>
    IReadOnlyList<StubRouteInfo> GetRoutes();

    /// <summary>
    /// Returns the current runtime state for all configured scenarios.
    /// </summary>
    IReadOnlyList<ScenarioStateInfo> GetScenarioStates();

    /// <summary>
    /// Resets all configured scenarios back to their initial state.
    /// </summary>
    void ResetScenarioStates();

    /// <summary>
    /// Resets the supplied configured scenario back to its initial state.
    /// </summary>
    /// <param name="scenarioName">The scenario name defined in YAML.</param>
    /// <returns><see langword="true"/> when the scenario exists in the active configuration; otherwise <see langword="false"/>.</returns>
    bool ResetScenarioState(string scenarioName);
}
