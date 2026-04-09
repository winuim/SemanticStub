using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace SemanticStub.Api.Services;

/// <summary>
/// Converts a loaded <see cref="StubDocument"/> into concrete <see cref="StubResponse"/> values by applying deterministic path, method, query, header, and body matching rules.
/// </summary>
public sealed class StubService : IStubService
{
    private static readonly string[] SupportedMethodOrder = [HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete];
    private static readonly Func<string, string> MissingResponseFileReader = _ => throw new InvalidOperationException("No response file reader configured.");
    private readonly Func<StubDocument> documentAccessor;
    private readonly StubResponseBuilder responseBuilder;
    private readonly IMatcherService matcherService;
    private readonly ScenarioService scenarioService;
    private readonly ISemanticMatcherService? semanticMatcherService;
    private readonly ILogger<StubService>? logger;

    /// <summary>
    /// Creates a service that loads its stub document immediately from the configured loader and uses the supplied matcher implementation for conditional matches.
    /// </summary>
    /// <param name="loader">Provides the validated stub document and any relative file-backed response payloads.</param>
    /// <param name="matcherService">The matcher used to evaluate <c>x-match</c> candidates when a route and method have been resolved.</param>
    /// <param name="scenarioService">Stores in-memory scenario transitions for responses that opt into <c>x-scenario</c>.</param>
    /// <exception cref="DirectoryNotFoundException">Propagated when the loader cannot locate the configured definitions directory.</exception>
    /// <exception cref="FileNotFoundException">Propagated when the loader cannot find required stub files or response files.</exception>
    /// <exception cref="InvalidOperationException">Propagated when the loader cannot build a valid stub document.</exception>
    public StubService(IStubDefinitionLoader loader, IMatcherService matcherService, ScenarioService scenarioService)
        : this(CreateLoadedDocumentAccessor(loader), loader.LoadResponseFileContent, matcherService, scenarioService, semanticMatcherService: null, logger: null)
    {
    }

    /// <summary>
    /// Creates a service that always evaluates requests against the latest successfully loaded stub document.
    /// </summary>
    /// <param name="state">Provides the current validated stub document snapshot and file-backed response payloads.</param>
    /// <param name="matcherService">The matcher used to evaluate <c>x-match</c> candidates when a route and method have been resolved.</param>
    /// <param name="scenarioService">Stores in-memory scenario transitions for responses that opt into <c>x-scenario</c>.</param>
    internal StubService(
        StubDefinitionState state,
        IMatcherService matcherService,
        ScenarioService scenarioService,
        ISemanticMatcherService semanticMatcherService,
        ILogger<StubService> logger)
        : this(state.GetCurrentDocument, state.LoadResponseFileContent, matcherService, scenarioService, semanticMatcherService, logger)
    {
    }

    /// <summary>
    /// Creates a service over an already loaded stub document and disables relative file-backed responses.
    /// </summary>
    /// <param name="document">The validated stub document to evaluate.</param>
    /// <param name="scenarioService">Stores in-memory scenario transitions for responses that opt into <c>x-scenario</c>.</param>
    /// <remarks>Relative <c>x-response-file</c> responses will fail at runtime because no response-file reader is configured by this overload.</remarks>
    public StubService(StubDocument document, ScenarioService scenarioService)
        : this(document, MissingResponseFileReader, new MatcherService(), scenarioService, semanticMatcherService: null, logger: null)
    {
    }

    /// <summary>
    /// Creates a service over an already loaded stub document and uses the supplied delegate to resolve relative file-backed responses.
    /// </summary>
    /// <param name="document">The validated stub document to evaluate.</param>
    /// <param name="responseFileReader">Loads the contents of a relative response file selected by the matching stub.</param>
    /// <param name="matcherService">The matcher used to evaluate <c>x-match</c> candidates when a route and method have been resolved.</param>
    /// <param name="scenarioService">Stores in-memory scenario transitions for responses that opt into <c>x-scenario</c>.</param>
    /// <param name="semanticMatcherService">When supplied, enables semantic fallback matching for candidates that define <c>x-semantic-match</c>.</param>
    /// <param name="logger">When supplied, emits structured log events for match selection and semantic scoring.</param>
    public StubService(
        StubDocument document,
        Func<string, string> responseFileReader,
        IMatcherService matcherService,
        ScenarioService scenarioService,
        ISemanticMatcherService? semanticMatcherService = null,
        ILogger<StubService>? logger = null)
        : this(() => document, responseFileReader, matcherService, scenarioService, semanticMatcherService, logger)
    {
    }

    private StubService(
        Func<StubDocument> documentAccessor,
        Func<string, string> responseFileReader,
        IMatcherService matcherService,
        ScenarioService scenarioService,
        ISemanticMatcherService? semanticMatcherService,
        ILogger<StubService>? logger)
    {
        this.documentAccessor = documentAccessor;
        this.responseBuilder = new StubResponseBuilder(responseFileReader);
        this.matcherService = matcherService;
        this.scenarioService = scenarioService;
        this.semanticMatcherService = semanticMatcherService;
        this.logger = logger;
    }

    private static Func<StubDocument> CreateLoadedDocumentAccessor(IStubDefinitionLoader loader)
    {
        var document = loader.LoadDefaultDefinition();
        return () => document;
    }

    /// <summary>
    /// Resolves a response for callers that only need method and path matching.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate.</param>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>.</param>
    /// <returns>The same result contract as the full overload, with query, header, and body matching treated as unspecified.</returns>
    public StubMatchResult TryGetResponse(string method, string path, out StubResponse? response)
    {
        return TryGetResponse(
            method,
            path,
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null,
            out response);
    }

    /// <summary>
    /// Returns the configured HTTP methods for the supplied path in a stable order suitable for the <c>Allow</c> response header.
    /// </summary>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <returns>The configured methods for the resolved path, or an empty list when no path matches.</returns>
    public IReadOnlyList<string> GetAllowedMethods(string path)
    {
        var pathItem = StubRouteResolver.ResolvePathItem(documentAccessor(), path);

        if (pathItem is null)
        {
            return Array.Empty<string>();
        }

        return SupportedMethodOrder
            .Where(method => StubOperationResolver.GetOperation(method, pathItem) is not null)
            .ToArray();
    }

    /// <summary>
    /// Resolves a response while considering query-based match conditions so more specific stubs can override broad defaults.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate.</param>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <param name="query">Single-value query parameters keyed by parameter name. Missing or empty dictionaries mean no query conditions are available to match.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>.</param>
    /// <returns>The same result contract as the full overload, with headers omitted and the body treated as unspecified.</returns>
    public StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, string> query, out StubResponse? response)
    {
        return TryGetResponse(
            method,
            path,
            ConvertQueryValues(query),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null,
            out response);
    }

    /// <summary>
    /// Resolves a response while considering query-based match conditions so more specific stubs can override broad defaults.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate.</param>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <param name="query">Query parameters keyed by parameter name, including repeated values in request order.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>.</param>
    /// <returns>The same result contract as the full overload, with headers omitted and the body treated as unspecified.</returns>
    public StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, StringValues> query, out StubResponse? response)
    {
        return TryGetResponse(
            method,
            path,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null,
            out response);
    }

    /// <summary>
    /// Resolves a response while considering query and body match conditions so structured request payloads can select a narrower stub.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate.</param>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <param name="query">Single-value query parameters keyed by parameter name.</param>
    /// <param name="body">The request body used for JSON body matching. <see langword="null"/> means no body conditions can match.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>.</param>
    /// <returns>The same result contract as the full overload, with headers omitted.</returns>
    public StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, string> query, string? body, out StubResponse? response)
    {
        return TryGetResponse(
            method,
            path,
            ConvertQueryValues(query),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body,
            out response);
    }

    /// <summary>
    /// Resolves a response while considering query and body match conditions so structured request payloads can select a narrower stub.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate.</param>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <param name="query">Query parameters keyed by parameter name, including repeated values in request order.</param>
    /// <param name="body">The request body used for JSON body matching. <see langword="null"/> means no body conditions can match.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>.</param>
    /// <returns>The same result contract as the full overload, with headers omitted.</returns>
    public StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, StringValues> query, string? body, out StubResponse? response)
    {
        return TryGetResponse(
            method,
            path,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body,
            out response);
    }

    /// <summary>
    /// Resolves the most specific stub response by evaluating path, method, query, headers, and body through the same matching pipeline.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate. Unsupported methods produce <see cref="StubMatchResult.MethodNotAllowed"/> only after a path match is found.</param>
    /// <param name="path">The absolute request path such as <c>/users</c>. Exact paths win before template paths.</param>
    /// <param name="query">Query parameters keyed by parameter name. Repeated values are matched in request order.</param>
    /// <param name="headers">Request headers keyed by header name. Supply a case-insensitive dictionary for HTTP semantics.</param>
    /// <param name="body">The request body used for JSON body matching. Invalid JSON does not throw here; it simply prevents structured body conditions from matching.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>; otherwise <see langword="null"/>.</param>
    /// <returns>
    /// <see cref="StubMatchResult.Matched"/> when a conditional or default response can be built,
    /// <see cref="StubMatchResult.PathNotFound"/> when no path matches,
    /// <see cref="StubMatchResult.MethodNotAllowed"/> when the path exists but the method does not,
    /// or <see cref="StubMatchResult.ResponseNotConfigured"/> when a route matches but the selected response is missing content or otherwise unusable.
    /// </returns>
    /// <remarks>When a response defines <c>x-scenario.next</c>, selecting that response advances the in-memory scenario state before returning. Relative file-backed responses are loaded through the configured response-file reader.</remarks>
    public StubMatchResult TryGetResponse(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        out StubResponse? response)
    {
        var dispatch = DispatchAsync(method, path, query, headers, body).GetAwaiter().GetResult();
        response = dispatch.Response;
        return dispatch.Result;
    }

    /// <inheritdoc/>
    public async Task<StubDispatchResult> DispatchAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        return await EvaluateAsync(
            method,
            path,
            query,
            headers,
            body,
            mutateScenarioState: true,
            includeCandidates: true,
            includeSemanticCandidates: false).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<MatchExplanationInfo> ExplainMatchAsync(MatchRequestInfo request)
    {
        var dispatch = await EvaluateAsync(
            StubRouteResolver.NormalizeMethod(request.Method),
            StubRouteResolver.NormalizePath(request.Path),
            ConvertQueryValues(request.Query),
            new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase),
            string.IsNullOrWhiteSpace(request.Body) ? null : request.Body,
            mutateScenarioState: false,
            includeCandidates: request.IncludeCandidates,
            includeSemanticCandidates: request.IncludeSemanticCandidates).ConfigureAwait(false);

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
        bool includeSemanticCandidates)
    {
        var document = documentAccessor();

        if (!StubOperationResolver.TryResolveOperation(document, method, path, out var pathPattern, out var pathItem, out var operation, out var failedMatchResult))
        {
            return CreateFailedDispatchResult(
                failedMatchResult,
                failedMatchResult == StubMatchResult.MethodNotAllowed
                    ? "The request path matched, but the HTTP method is not configured for that route."
                    : "No configured route matched the supplied request path.",
                pathMatched: failedMatchResult == StubMatchResult.MethodNotAllowed,
                methodMatched: false);
        }

        if (OperationUsesScenario(operation))
        {
            return await scenarioService.ExecuteLockedAsync(
                () => DispatchCoreAsync(method, path, pathPattern, pathItem, operation, query, headers, body, mutateScenarioState, includeCandidates, includeSemanticCandidates)).ConfigureAwait(false);
        }

        return await DispatchCoreAsync(method, path, pathPattern, pathItem, operation, query, headers, body, mutateScenarioState, includeCandidates, includeSemanticCandidates).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<(StubMatchResult Result, StubResponse? Response)> TryGetResponseAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        var dispatch = await DispatchAsync(method, path, query, headers, body).ConfigureAwait(false);
        return (dispatch.Result, dispatch.Response);
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
        bool includeSemanticCandidates)
    {
        var routeId = GetRouteId(method, pathPattern, operation);
        var request = CreateInspectionRequest(method, path, query, headers, body, includeCandidates, includeSemanticCandidates);
        var scenarioSnapshots = GetScenarioSnapshots(operation);
        var deterministicEvaluations = matcherService.EvaluateCandidates(pathItem.Parameters, operation, query, headers, body)
            .Select((evaluation, index) => CreateCandidateInfo(evaluation, index, scenarioSnapshots))
            .ToList();
        var selectedDeterministicCandidate = matcherService.FindBestMatch(
            pathItem.Parameters,
            operation,
            query,
            headers,
            body,
            candidate => scenarioService.IsMatch(candidate.Response.Scenario) && IsDeterministicCandidate(candidate));

        if (selectedDeterministicCandidate is not null)
        {
            var matchedCandidate = deterministicEvaluations.First(candidate =>
                ReferenceEquals(operation.Matches[candidate.CandidateIndex], selectedDeterministicCandidate));

            if (!responseBuilder.TryBuild(selectedDeterministicCandidate.Response, out var deterministicResponse))
            {
                return CreateMatchedDispatchResult(
                    request,
                    routeId,
                    method,
                    pathPattern,
                    deterministicEvaluations,
                    semanticEvaluation: null,
                    StubMatchResult.ResponseNotConfigured,
                    response: null,
                    matchMode: "exact",
                    selectedResponseId: matchedCandidate.ResponseId,
                    selectedResponseStatusCode: matchedCandidate.ResponseStatusCode,
                    selectionReason: $"Deterministic candidate {matchedCandidate.CandidateIndex} matched, but its response is not configured.");
            }

            logger?.LogInformation(
                "Deterministic conditional match selected for '{Path}' {Method}. QueryKeys={QueryKeys}, HeaderKeys={HeaderKeys}, HasBody={HasBody}.",
                path,
                method.ToUpperInvariant(),
                selectedDeterministicCandidate.Query.Count + selectedDeterministicCandidate.PartialQuery.Count + selectedDeterministicCandidate.RegexQuery.Count,
                selectedDeterministicCandidate.Headers.Count,
                selectedDeterministicCandidate.Body is not null);

            if (mutateScenarioState)
            {
                scenarioService.Advance(selectedDeterministicCandidate.Response.Scenario);
            }

            return CreateMatchedDispatchResult(
                request,
                routeId,
                method,
                pathPattern,
                deterministicEvaluations,
                semanticEvaluation: null,
                StubMatchResult.Matched,
                deterministicResponse,
                matchMode: "exact",
                selectedResponseId: matchedCandidate.ResponseId,
                selectedResponseStatusCode: matchedCandidate.ResponseStatusCode,
                selectionReason: $"Deterministic candidate {matchedCandidate.CandidateIndex} matched all configured conditions and was selected.");
        }

        SemanticMatchInfo? semanticEvaluationInfo = null;
        var semanticExplanation = semanticMatcherService is null
            ? new SemanticMatchExplanation()
            : await semanticMatcherService.ExplainMatchAsync(
                method,
                path,
                query,
                headers,
                body,
                operation.Matches,
                candidate => scenarioService.IsMatch(candidate.Response.Scenario),
                includeCandidateScores: includeSemanticCandidates).ConfigureAwait(false);

        if (semanticExplanation.Attempted)
        {
            semanticEvaluationInfo = CreateSemanticMatchInfo(semanticExplanation, operation, includeSemanticCandidates);
        }

        if (semanticExplanation.SelectedCandidate is not null)
        {
            var selectedCandidate = deterministicEvaluations.FirstOrDefault(candidate =>
                    ReferenceEquals(operation.Matches[candidate.CandidateIndex], semanticExplanation.SelectedCandidate))
                ?? CreateCandidateInfo(
                    new QueryMatchCandidateEvaluation
                    {
                        Candidate = semanticExplanation.SelectedCandidate,
                        QueryMatched = false,
                        HeaderMatched = false,
                        BodyMatched = false,
                    },
                    operation.Matches.FindIndex(candidate => ReferenceEquals(candidate, semanticExplanation.SelectedCandidate)),
                    scenarioSnapshots);

            if (!responseBuilder.TryBuild(semanticExplanation.SelectedCandidate.Response, out var semanticResponse))
            {
                return CreateMatchedDispatchResult(
                    request,
                    routeId,
                    method,
                    pathPattern,
                    deterministicEvaluations,
                    semanticEvaluationInfo,
                    StubMatchResult.ResponseNotConfigured,
                    response: null,
                    matchMode: "semantic",
                    selectedResponseId: selectedCandidate.ResponseId,
                    selectedResponseStatusCode: selectedCandidate.ResponseStatusCode,
                    selectionReason: "Semantic fallback selected a candidate, but its response is not configured.");
            }

            logger?.LogInformation(
                "Semantic fallback produced a match for '{Path}' {Method}.",
                path,
                method.ToUpperInvariant());

            if (mutateScenarioState)
            {
                scenarioService.Advance(semanticExplanation.SelectedCandidate.Response.Scenario);
            }

            return CreateMatchedDispatchResult(
                request,
                routeId,
                method,
                pathPattern,
                deterministicEvaluations,
                semanticEvaluationInfo,
                StubMatchResult.Matched,
                semanticResponse,
                matchMode: "semantic",
                selectedResponseId: selectedCandidate.ResponseId,
                selectedResponseStatusCode: selectedCandidate.ResponseStatusCode,
                selectionReason: "Semantic fallback selected the highest-scoring eligible candidate.");
        }

        if (TryBuildDefaultOperationResponse(operation, mutateScenarioState, out var response))
        {
            var defaultResponse = GetDefaultResponse(operation, scenarioSnapshots);
            return CreateMatchedDispatchResult(
                request,
                routeId,
                method,
                pathPattern,
                deterministicEvaluations,
                semanticEvaluationInfo,
                StubMatchResult.Matched,
                response,
                matchMode: "fallback",
                selectedResponseId: defaultResponse?.ResponseId,
                selectedResponseStatusCode: defaultResponse?.StatusCode,
                selectionReason: "No conditional candidate matched, so the eligible default response was selected.");
        }

        return CreateMatchedDispatchResult(
            request,
            routeId,
            method,
            pathPattern,
            deterministicEvaluations,
            semanticEvaluationInfo,
            StubMatchResult.ResponseNotConfigured,
            response: null,
            matchMode: null,
            selectedResponseId: null,
            selectedResponseStatusCode: null,
            selectionReason: "The route matched, but no eligible conditional or default response is configured for the current request and scenario state.");
    }

    private static IReadOnlyDictionary<string, StringValues> ConvertQueryValues(IReadOnlyDictionary<string, string> query)
    {
        return query.ToDictionary(
            entry => entry.Key,
            entry => new StringValues(entry.Value),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, StringValues> ConvertQueryValues(IReadOnlyDictionary<string, string[]> query)
    {
        return query.ToDictionary(
            entry => entry.Key,
            entry => new StringValues(entry.Value ?? Array.Empty<string>()),
            StringComparer.Ordinal);
    }

    private QueryMatchEvaluationResult TryBuildMatchedQueryResponse(
        string method,
        string path,
        PathItemDefinition pathItem,
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        out StubResponse response)
    {
        response = null!;

        if (operation.Matches.Count == 0)
        {
            return QueryMatchEvaluationResult.NoMatch;
        }

        // Query/header/body conditions are combined, then the most specific surviving candidate wins.
        var matchedCandidate = matcherService.FindBestMatch(
            pathItem.Parameters,
            operation,
            query,
            headers,
            body,
            candidate => scenarioService.IsMatch(candidate.Response.Scenario) && IsDeterministicCandidate(candidate));

        if (matchedCandidate is null)
        {
            return QueryMatchEvaluationResult.NoMatch;
        }

        if (!responseBuilder.TryBuild(matchedCandidate.Response, out response))
        {
            return QueryMatchEvaluationResult.MatchedButInvalidResponse;
        }

        logger?.LogInformation(
            "Deterministic conditional match selected for '{Path}' {Method}. QueryKeys={QueryKeys}, HeaderKeys={HeaderKeys}, HasBody={HasBody}.",
            path,
            method.ToUpperInvariant(),
            matchedCandidate.Query.Count + matchedCandidate.PartialQuery.Count + matchedCandidate.RegexQuery.Count,
            matchedCandidate.Headers.Count,
            matchedCandidate.Body is not null);

        scenarioService.Advance(matchedCandidate.Response.Scenario);

        return QueryMatchEvaluationResult.Matched;
    }

    private static bool IsDeterministicCandidate(QueryMatchDefinition candidate)
    {
        return candidate.Query.Count > 0 ||
               candidate.PartialQuery.Count > 0 ||
               candidate.RegexQuery.Count > 0 ||
               candidate.Headers.Count > 0 ||
               candidate.Body is not null;
    }

    private static StubDispatchResult CreateFailedDispatchResult(
        StubMatchResult result,
        string selectionReason,
        bool pathMatched,
        bool methodMatched)
    {
        return new StubDispatchResult
        {
            Result = result,
            Explanation = new MatchExplanationInfo
            {
                PathMatched = pathMatched,
                MethodMatched = methodMatched,
                SelectionReason = selectionReason,
                Result = new MatchSimulationInfo
                {
                    Matched = false,
                    MatchResult = result.ToString(),
                }
            }
        };
    }

    private static StubDispatchResult CreateMatchedDispatchResult(
        MatchRequestInfo request,
        string routeId,
        string method,
        string pathPattern,
        IReadOnlyList<MatchCandidateInfo> deterministicCandidates,
        SemanticMatchInfo? semanticEvaluation,
        StubMatchResult matchResult,
        StubResponse? response,
        string? matchMode,
        string? selectedResponseId,
        int? selectedResponseStatusCode,
        string selectionReason)
    {
        return new StubDispatchResult
        {
            Result = matchResult,
            Response = response,
            Explanation = new MatchExplanationInfo
            {
                PathMatched = true,
                MethodMatched = true,
                SelectionReason = selectionReason,
                DeterministicCandidates = deterministicCandidates,
                SemanticEvaluation = semanticEvaluation,
                Result = new MatchSimulationInfo
                {
                    Matched = matchResult == StubMatchResult.Matched,
                    MatchResult = matchResult.ToString(),
                    RouteId = routeId,
                    Method = method,
                    PathPattern = pathPattern,
                    SelectedResponseId = selectedResponseId,
                    SelectedResponseStatusCode = selectedResponseStatusCode,
                    MatchMode = matchMode,
                    Candidates = request.IncludeCandidates ? deterministicCandidates : [],
                }
            }
        };
    }

    private static MatchRequestInfo CreateInspectionRequest(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        bool includeCandidates,
        bool includeSemanticCandidates)
    {
        return new MatchRequestInfo
        {
            Method = method,
            Path = path,
            Query = query.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Select(value => value ?? string.Empty).ToArray(),
                StringComparer.Ordinal),
            Headers = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase),
            Body = body,
            IncludeCandidates = includeCandidates,
            IncludeSemanticCandidates = includeSemanticCandidates,
        };
    }

    private IReadOnlyDictionary<string, ScenarioStateSnapshot> GetScenarioSnapshots(OperationDefinition operation)
    {
        var scenarioNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var response in operation.Responses.Values)
        {
            if (response.Scenario is not null)
            {
                scenarioNames.Add(response.Scenario.Name);
            }
        }

        foreach (var match in operation.Matches)
        {
            if (match.Response.Scenario is not null)
            {
                scenarioNames.Add(match.Response.Scenario.Name);
            }
        }

        return scenarioNames.ToDictionary(
            scenarioName => scenarioName,
            scenarioName => scenarioService.GetSnapshotWithinLock(scenarioName),
            StringComparer.Ordinal);
    }

    private static MatchCandidateInfo CreateCandidateInfo(
        QueryMatchCandidateEvaluation evaluation,
        int index,
        IReadOnlyDictionary<string, ScenarioStateSnapshot> scenarioSnapshots)
    {
        int? responseStatusCode = evaluation.Candidate.Response.StatusCode > 0
            ? evaluation.Candidate.Response.StatusCode
            : null;
        var scenarioMatched = IsScenarioMatch(evaluation.Candidate.Response.Scenario, scenarioSnapshots);
        var responseConfigured = IsConfiguredResponse(evaluation.Candidate.Response);

        return new MatchCandidateInfo
        {
            CandidateIndex = index,
            QueryMatched = evaluation.QueryMatched,
            HeaderMatched = evaluation.HeaderMatched,
            BodyMatched = evaluation.BodyMatched,
            ScenarioMatched = scenarioMatched,
            ResponseConfigured = responseConfigured,
            Matched = evaluation.Matched && scenarioMatched,
            ResponseId = responseStatusCode?.ToString(),
            ResponseStatusCode = responseStatusCode,
        };
    }

    private static SemanticMatchInfo CreateSemanticMatchInfo(
        SemanticMatchExplanation explanation,
        OperationDefinition operation,
        bool includeCandidates)
    {
        return new SemanticMatchInfo
        {
            Attempted = explanation.Attempted,
            Threshold = explanation.Threshold,
            RequiredMargin = explanation.RequiredMargin,
            SelectedScore = explanation.SelectedScore,
            SecondBestScore = explanation.SecondBestScore,
            MarginToSecondBest = explanation.MarginToSecondBest,
            Candidates = includeCandidates
                ? explanation.CandidateScores
                    .Select(score => new SemanticCandidateInfo
                    {
                        CandidateIndex = operation.Matches.FindIndex(candidate => ReferenceEquals(candidate, score.Candidate)),
                        Eligible = score.Eligible,
                        Score = score.Score,
                        AboveThreshold = score.AboveThreshold,
                    })
                    .ToList()
                : [],
        };
    }

    private static string GetRouteId(string method, string pathPattern, OperationDefinition operation)
    {
        return string.IsNullOrEmpty(operation.OperationId)
            ? $"{method}:{pathPattern}"
            : operation.OperationId;
    }

    private static bool IsConfiguredResponse(QueryMatchResponseDefinition response)
    {
        return response.StatusCode > 0 &&
               (!string.IsNullOrEmpty(response.ResponseFile) || response.Content.Count > 0);
    }

    private static bool IsConfiguredResponse(ResponseDefinition response)
    {
        return !string.IsNullOrEmpty(response.ResponseFile) || response.Content.Count > 0;
    }

    private static bool IsScenarioMatch(
        ScenarioDefinition? scenario,
        IReadOnlyDictionary<string, ScenarioStateSnapshot> scenarioSnapshots)
    {
        if (scenario is null)
        {
            return true;
        }

        var currentState = scenarioSnapshots.TryGetValue(scenario.Name, out var snapshot)
            ? snapshot.State
            : "initial";

        return string.Equals(currentState, scenario.State, StringComparison.Ordinal);
    }

    private static (string ResponseId, int StatusCode)? GetDefaultResponse(
        OperationDefinition operation,
        IReadOnlyDictionary<string, ScenarioStateSnapshot> scenarioSnapshots)
    {
        foreach (var response in operation.Responses)
        {
            if (!int.TryParse(response.Key, out var statusCode))
            {
                continue;
            }

            if (!IsScenarioMatch(response.Value.Scenario, scenarioSnapshots) || !IsConfiguredResponse(response.Value))
            {
                continue;
            }

            return (response.Key, statusCode);
        }

        return null;
    }

    private bool TryBuildDefaultOperationResponse(OperationDefinition operation, bool mutateScenarioState, out StubResponse response)
    {
        response = null!;

        var matchedResponse = operation.Responses
            .FirstOrDefault(entry => IsEligibleDefaultResponse(entry.Key, entry.Value));

        if (string.IsNullOrEmpty(matchedResponse.Key) || !int.TryParse(matchedResponse.Key, out var statusCode))
        {
            return false;
        }

        var built = responseBuilder.TryBuild(statusCode, matchedResponse.Value, out response);

        if (built && mutateScenarioState)
        {
            scenarioService.Advance(matchedResponse.Value.Scenario);
        }

        return built;
    }

    private bool IsEligibleDefaultResponse(string statusCode, ResponseDefinition responseDefinition)
    {
        return int.TryParse(statusCode, out _) &&
               scenarioService.IsMatch(responseDefinition.Scenario) &&
               (responseDefinition.Content.Count > 0 || !string.IsNullOrEmpty(responseDefinition.ResponseFile));
    }

}
