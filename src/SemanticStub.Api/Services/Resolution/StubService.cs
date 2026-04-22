using SemanticStub.Application.Infrastructure.Yaml;
using SemanticStub.Infrastructure.Yaml;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Models;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services;
using Microsoft.Extensions.Primitives;

namespace SemanticStub.Api.Services;

/// <summary>
/// Converts a loaded <see cref="StubDocument"/> into concrete <see cref="StubResponse"/> values by applying deterministic path, method, query, header, and body matching rules.
/// </summary>
public sealed class StubService : IStubService
{
    private static readonly string[] SupportedMethodOrder = [HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete];
    private readonly Func<StubDocument> _documentAccessor;
    private readonly StubDispatchSelector _dispatchSelector;
    private readonly StubInspectionProjectionBuilder _inspectionProjectionBuilder;
    private readonly MatcherService _matcherService;
    private readonly ScenarioService _scenarioService;

    /// <summary>
    /// Creates a service that always evaluates requests against the latest successfully loaded stub document.
    /// </summary>
    /// <param name="state">Provides the current validated stub document snapshot and file-backed response payloads.</param>
    /// <param name="matcherService">The matcher used to evaluate <c>x-match</c> candidates when a route and method have been resolved.</param>
    /// <param name="scenarioService">Stores in-memory scenario transitions for responses that opt into <c>x-scenario</c>.</param>
    internal StubService(
        StubDefinitionState state,
        MatcherService matcherService,
        ScenarioService scenarioService,
        StubDispatchSelector dispatchSelector,
        StubInspectionProjectionBuilder inspectionProjectionBuilder)
        : this(state.GetCurrentDocument, matcherService, scenarioService, dispatchSelector, inspectionProjectionBuilder)
    {
    }

    private StubService(
        Func<StubDocument> documentAccessor,
        MatcherService matcherService,
        ScenarioService scenarioService,
        StubDispatchSelector dispatchSelector,
        StubInspectionProjectionBuilder inspectionProjectionBuilder)
    {
        _documentAccessor = documentAccessor;
        _dispatchSelector = dispatchSelector;
        _inspectionProjectionBuilder = inspectionProjectionBuilder;
        _matcherService = matcherService;
        _scenarioService = scenarioService;
    }

    /// <summary>
    /// Returns the configured HTTP methods for the supplied path in a stable order suitable for the <c>Allow</c> response header.
    /// </summary>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <returns>The configured methods for the resolved path, or an empty list when no path matches.</returns>
    public IReadOnlyList<string> GetAllowedMethods(string path)
    {
        var pathItem = StubRouteResolver.ResolvePathItem(_documentAccessor(), path);

        if (pathItem is null)
        {
            return Array.Empty<string>();
        }

        return SupportedMethodOrder
            .Where(method => StubOperationResolver.GetOperation(method, pathItem) is not null)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<StubDispatchResult> DispatchAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        CancellationToken cancellationToken = default)
    {
        return await EvaluateAsync(
            method,
            path,
            query,
            headers,
            body,
            mutateScenarioState: true,
            includeCandidates: true,
            includeSemanticCandidates: false,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<MatchExplanationInfo> ExplainMatchAsync(MatchRequestInfo request, CancellationToken cancellationToken = default)
    {
        var dispatch = await EvaluateAsync(
            CreateExplainMethod(request),
            CreateExplainPath(request),
            ConvertQueryValues(request.Query),
            CreateHeaders(request.Headers),
            NormalizeBody(request.Body),
            mutateScenarioState: false,
            includeCandidates: request.IncludeCandidates,
            includeSemanticCandidates: request.IncludeSemanticCandidates,
            cancellationToken);

        return dispatch.Explanation;
    }

    private async Task<StubDispatchResult> EvaluateAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        bool mutateScenarioState,
        bool includeCandidates,
        bool includeSemanticCandidates,
        CancellationToken cancellationToken)
    {
        var document = _documentAccessor();

        if (!StubOperationResolver.TryResolveOperation(document, method, path, out var pathPattern, out var pathItem, out var operation, out var failedMatchResult))
        {
            return StubMatchExplanationBuilder.CreateFailedDispatchResult(
                failedMatchResult,
                failedMatchResult == StubMatchResult.MethodNotAllowed
                    ? "The request path matched, but the HTTP method is not configured for that route."
                    : "No configured route matched the supplied request path.",
                pathMatched: failedMatchResult == StubMatchResult.MethodNotAllowed,
                methodMatched: false);
        }

        if (OperationUsesScenario(operation))
        {
            return await _scenarioService.ExecuteLockedAsync(
                () => DispatchCoreAsync(method, path, pathPattern, pathItem, operation, query, headers, body, mutateScenarioState, includeCandidates, includeSemanticCandidates, cancellationToken),
                cancellationToken);
        }

        return await DispatchCoreAsync(method, path, pathPattern, pathItem, operation, query, headers, body, mutateScenarioState, includeCandidates, includeSemanticCandidates, cancellationToken);
    }

    private static bool OperationUsesScenario(OperationDefinition operation)
    {
        return operation.Matches.Any(match => match.Response.Scenario is not null) ||
               operation.Responses.Values.Any(response => response.Scenario is not null);
    }

    private async Task<StubDispatchResult> DispatchCoreAsync(
        string method,
        string path,
        string pathPattern,
        PathItemDefinition pathItem,
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        bool mutateScenarioState,
        bool includeCandidates,
        bool includeSemanticCandidates,
        CancellationToken cancellationToken)
    {
        var routeId = GetRouteId(method, pathPattern, operation);
        var request = _inspectionProjectionBuilder.CreateInspectionRequest(method, path, query, headers, body, includeCandidates, includeSemanticCandidates);
        var scenarioSnapshots = _inspectionProjectionBuilder.GetScenarioSnapshots(operation);
        var deterministicEvaluations = _matcherService.EvaluateCandidates(pathItem.Parameters, operation, query, headers, body)
            .Select((evaluation, index) => _inspectionProjectionBuilder.CreateCandidateInfo(evaluation, index, scenarioSnapshots))
            .ToList();
        var selection = await _dispatchSelector.SelectAsync(
            method,
            path,
            pathItem,
            operation,
            query,
            headers,
            body,
            mutateScenarioState,
            includeSemanticCandidates,
            cancellationToken);

        SemanticMatchInfo? semanticEvaluationInfo = null;
        if (selection.SemanticExplanation.Attempted)
        {
            semanticEvaluationInfo = _inspectionProjectionBuilder.CreateSemanticMatchInfo(selection.SemanticExplanation, operation, includeSemanticCandidates);
        }

        string? selectedResponseId = selection.SelectedResponseId;
        int? selectedResponseStatusCode = selection.SelectedResponseStatusCode;
        string? selectedResponseSource = selection.SelectedResponseSource;
        int? selectedResponseCandidateIndex = selection.SelectedResponseCandidateIndex;

        if (selection.SelectedCandidate is not null)
        {
            var selectedCandidate = deterministicEvaluations.FirstOrDefault(candidate =>
                    ReferenceEquals(operation.Matches[candidate.CandidateIndex], selection.SelectedCandidate))
                ?? _inspectionProjectionBuilder.CreateCandidateInfo(
                    new QueryMatchCandidateEvaluation
                    {
                        Candidate = selection.SelectedCandidate,
                        QueryMatched = false,
                        HeaderMatched = false,
                        BodyMatched = false,
                    },
                    operation.Matches.FindIndex(candidate => ReferenceEquals(candidate, selection.SelectedCandidate)),
                    scenarioSnapshots);

            selectedResponseId = selectedCandidate.ResponseId;
            selectedResponseStatusCode = selectedCandidate.ResponseStatusCode;
        }

        return StubMatchExplanationBuilder.CreateMatchedDispatchResult(
            request,
            routeId,
            method,
            pathPattern,
            deterministicEvaluations,
            semanticEvaluationInfo,
            selection.Result,
            selection.Response,
            selection.MatchMode,
            selectedResponseId,
            selectedResponseStatusCode,
            selectedResponseSource,
            selectedResponseCandidateIndex,
            selection.SelectionReason);
    }

    private static IReadOnlyDictionary<string, StringValues> ConvertQueryValues(IReadOnlyDictionary<string, string[]> query)
    {
        return query.ToDictionary(
            entry => entry.Key,
            entry => new StringValues(entry.Value ?? Array.Empty<string>()),
            StringComparer.Ordinal);
    }

    private static string GetRouteId(string method, string pathPattern, OperationDefinition operation)
    {
        return string.IsNullOrEmpty(operation.OperationId)
            ? $"{method}:{pathPattern}"
            : operation.OperationId;
    }

    private static IReadOnlyDictionary<string, string> CreateHeaders(IReadOnlyDictionary<string, string> headers)
    {
        return new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
    }

    private static string CreateExplainMethod(MatchRequestInfo request)
    {
        return StubRouteResolver.NormalizeMethod(request.Method);
    }

    private static string CreateExplainPath(MatchRequestInfo request)
    {
        return StubRouteResolver.NormalizePath(request.Path);
    }

    private static string? NormalizeBody(string? body)
    {
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

}
