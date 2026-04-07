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
    /// Returns the effective runtime details for a single active route when it exists.
    /// </summary>
    /// <param name="routeId">The stable route identifier.</param>
    StubRouteDetailInfo? GetRoute(string routeId);

    /// <summary>
    /// Returns the current runtime state for all configured scenarios.
    /// </summary>
    IReadOnlyList<ScenarioStateInfo> GetScenarioStates();

    /// <summary>
    /// Returns the aggregate runtime metrics captured for real requests handled by the current process.
    /// </summary>
    RuntimeMetricsSummaryInfo GetRuntimeMetrics();

    /// <summary>
    /// Simulates how the runtime would match the supplied virtual request without executing a response or mutating scenario state.
    /// </summary>
    /// <param name="request">The virtual request to evaluate.</param>
    Task<MatchSimulationInfo> TestMatchAsync(MatchRequestInfo request);

    /// <summary>
    /// Explains how the runtime evaluated the supplied virtual request without executing a response or mutating scenario state.
    /// </summary>
    /// <param name="request">The virtual request to evaluate.</param>
    Task<MatchExplanationInfo> ExplainMatchAsync(MatchRequestInfo request);

    /// <summary>
    /// Returns the explanation captured for the most recent real request, when one has been recorded.
    /// </summary>
    MatchExplanationInfo? GetLastMatchExplanation();

    /// <summary>
    /// Captures the explanation for the most recent real request so it can be retrieved later.
    /// </summary>
    /// <param name="explanation">The explanation captured from the real request dispatch.</param>
    void RecordLastMatchExplanation(MatchExplanationInfo explanation);

    /// <summary>
    /// Records aggregate runtime metrics for one real request handled by the active runtime pipeline.
    /// </summary>
    /// <param name="explanation">The explanation captured from the same dispatch evaluation.</param>
    /// <param name="statusCode">The final HTTP status code returned to the caller.</param>
    /// <param name="elapsed">The end-to-end elapsed time observed by the controller for the request.</param>
    void RecordRequestMetrics(MatchExplanationInfo explanation, int statusCode, TimeSpan elapsed);

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
