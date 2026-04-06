using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

internal sealed class StubInspectionService : IStubInspectionService
{
    private readonly StubDefinitionState state;
    private readonly IStubDefinitionLoader loader;
    private readonly IOptions<StubSettings> settings;
    private readonly ScenarioService scenarioService;
    private readonly IMatcherService matcherService;
    private readonly ISemanticMatcherService semanticMatcherService;
    private readonly object lastMatchSyncRoot = new();
    private MatchExplanationInfo? lastMatchExplanation;

    public StubInspectionService(
        StubDefinitionState state,
        IStubDefinitionLoader loader,
        IOptions<StubSettings> settings,
        ScenarioService scenarioService,
        IMatcherService matcherService,
        ISemanticMatcherService semanticMatcherService)
    {
        this.state = state;
        this.loader = loader;
        this.settings = settings;
        this.scenarioService = scenarioService;
        this.matcherService = matcherService;
        this.semanticMatcherService = semanticMatcherService;
    }

    /// <inheritdoc/>
    public StubConfigSnapshot GetConfigSnapshot()
    {
        var document = state.GetCurrentDocument();
        var routes = BuildRoutes(document);

        return new StubConfigSnapshot
        {
            SnapshotTimestamp = DateTimeOffset.UtcNow,
            ConfigurationHash = ComputeDocumentHash(document),
            DefinitionsDirectoryPath = loader.GetDefinitionsDirectoryPath(),
            RouteCount = routes.Count,
            SemanticMatchingEnabled = settings.Value.SemanticMatching.Enabled,
        };
    }

    /// <inheritdoc/>
    public IReadOnlyList<StubRouteInfo> GetRoutes()
    {
        var document = state.GetCurrentDocument();
        return BuildRoutes(document);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ScenarioStateInfo> GetScenarioStates()
    {
        return scenarioService.ExecuteLocked(() =>
        {
            var document = state.GetCurrentDocument();
            var scenarioNames = GetScenarioNames(document);

            return scenarioNames
                .Select(name =>
                {
                    var snapshot = scenarioService.GetSnapshotWithinLock(name);
                    return new ScenarioStateInfo
                    {
                        Name = name,
                        CurrentState = snapshot.State,
                        LastUpdatedTimestamp = snapshot.LastUpdatedTimestamp,
                    };
                })
                .ToList();
        });
    }

    /// <inheritdoc/>
    public async Task<MatchSimulationInfo> TestMatchAsync(MatchRequestInfo request)
    {
        return (await EvaluateMatchAsync(request).ConfigureAwait(false)).Result;
    }

    /// <inheritdoc/>
    public Task<MatchExplanationInfo> ExplainMatchAsync(MatchRequestInfo request)
    {
        return EvaluateMatchAsync(request);
    }

    /// <inheritdoc/>
    public MatchExplanationInfo? GetLastMatchExplanation()
    {
        lock (lastMatchSyncRoot)
        {
            return lastMatchExplanation;
        }
    }

    /// <inheritdoc/>
    public async Task RecordLastMatchAsync(MatchRequestInfo request)
    {
        var explanation = await EvaluateMatchAsync(request).ConfigureAwait(false);

        lock (lastMatchSyncRoot)
        {
            lastMatchExplanation = explanation;
        }
    }

    /// <inheritdoc/>
    public void ResetScenarioStates()
    {
        scenarioService.ExecuteLocked(() =>
        {
            var document = state.GetCurrentDocument();
            scenarioService.ResetScenariosWithinLock(GetScenarioNames(document), DateTimeOffset.UtcNow);
            return 0;
        });
    }

    /// <inheritdoc/>
    public bool ResetScenarioState(string scenarioName)
    {
        return scenarioService.ExecuteLocked(() =>
        {
            var document = state.GetCurrentDocument();
            var scenarioNames = GetScenarioNames(document);

            if (!scenarioNames.Contains(scenarioName, StringComparer.Ordinal))
            {
                return false;
            }

            scenarioService.ResetScenarioWithinLock(scenarioName, DateTimeOffset.UtcNow);
            return true;
        });
    }

    private static IReadOnlyList<StubRouteInfo> BuildRoutes(StubDocument document)
    {
        var routes = new List<StubRouteInfo>();

        foreach (var (path, pathItem) in document.Paths)
        {
            var operations = new (string Method, OperationDefinition? Op)[]
            {
                ("GET", pathItem.Get),
                ("POST", pathItem.Post),
                ("PUT", pathItem.Put),
                ("PATCH", pathItem.Patch),
                ("DELETE", pathItem.Delete),
            };

            foreach (var (method, op) in operations)
            {
                if (op is null) continue;

                routes.Add(new StubRouteInfo
                {
                    RouteId = string.IsNullOrEmpty(op.OperationId)
                        ? $"{method}:{path}"
                        : op.OperationId,
                    Method = method,
                    PathPattern = path,
                    UsesSemanticMatching = HasSemanticMatch(op),
                    UsesScenario = HasScenario(op),
                    ResponseCount = op.Responses.Count,
                });
            }
        }

        return routes;
    }

    private async Task<MatchExplanationInfo> EvaluateMatchAsync(MatchRequestInfo request)
    {
        var method = NormalizeMethod(request.Method);
        var path = NormalizePath(request.Path);
        var query = ConvertQueryValues(request.Query);
        var headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase);
        var body = string.IsNullOrWhiteSpace(request.Body) ? null : request.Body;
        var document = state.GetCurrentDocument();
        var resolvedPath = ResolvePath(document, path);

        if (resolvedPath is null)
        {
            return CreateFailureExplanation(
                pathMatched: false,
                methodMatched: false,
                StubMatchResult.PathNotFound,
                "No configured route matched the supplied request path.");
        }

        var (pathPattern, pathItem) = resolvedPath.Value;
        var operation = GetOperation(method, pathItem);

        if (operation is null)
        {
            return CreateFailureExplanation(
                pathMatched: true,
                methodMatched: false,
                StubMatchResult.MethodNotAllowed,
                "The request path matched, but the HTTP method is not configured for that route.");
        }

        var routeId = GetRouteId(method, pathPattern, operation);
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
            candidate => IsScenarioMatch(candidate.Response.Scenario, scenarioSnapshots) && IsDeterministicCandidate(candidate));

        if (selectedDeterministicCandidate is not null)
        {
            var matchedCandidate = deterministicEvaluations.First(candidate =>
                ReferenceEquals(operation.Matches[candidate.CandidateIndex], selectedDeterministicCandidate));

            if (!matchedCandidate.ResponseConfigured)
            {
                return CreateSuccessExplanation(
                    request,
                    routeId,
                    method,
                    pathPattern,
                    deterministicEvaluations,
                    semanticEvaluation: null,
                    matched: false,
                    matchResult: StubMatchResult.ResponseNotConfigured,
                    matchMode: "exact",
                    selectedResponseId: matchedCandidate.ResponseId,
                    selectedResponseStatusCode: matchedCandidate.ResponseStatusCode,
                    selectionReason: $"Deterministic candidate {matchedCandidate.CandidateIndex} matched, but its response is not configured.");
            }

            return CreateSuccessExplanation(
                request,
                routeId,
                method,
                pathPattern,
                deterministicEvaluations,
                semanticEvaluation: null,
                matched: true,
                matchResult: StubMatchResult.Matched,
                matchMode: "exact",
                selectedResponseId: matchedCandidate.ResponseId,
                selectedResponseStatusCode: matchedCandidate.ResponseStatusCode,
                selectionReason: $"Deterministic candidate {matchedCandidate.CandidateIndex} matched all configured conditions and was selected.");
        }

        SemanticMatchInfo? semanticEvaluationInfo = null;
        var semanticExplanation = await semanticMatcherService.ExplainMatchAsync(
            method,
            path,
            query,
            headers,
            body,
            operation.Matches,
            candidate => IsScenarioMatch(candidate.Response.Scenario, scenarioSnapshots),
            request.IncludeSemanticCandidates).ConfigureAwait(false);

        if (semanticExplanation.Attempted)
        {
            semanticEvaluationInfo = CreateSemanticMatchInfo(semanticExplanation, operation, request.IncludeSemanticCandidates);
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

            if (!selectedCandidate.ResponseConfigured)
            {
                return CreateSuccessExplanation(
                    request,
                    routeId,
                    method,
                    pathPattern,
                    deterministicEvaluations,
                    semanticEvaluationInfo,
                    matched: false,
                    matchResult: StubMatchResult.ResponseNotConfigured,
                    matchMode: "semantic",
                    selectedResponseId: selectedCandidate.ResponseId,
                    selectedResponseStatusCode: selectedCandidate.ResponseStatusCode,
                    selectionReason: "Semantic fallback selected a candidate, but its response is not configured.");
            }

            return CreateSuccessExplanation(
                request,
                routeId,
                method,
                pathPattern,
                deterministicEvaluations,
                semanticEvaluationInfo,
                matched: true,
                matchResult: StubMatchResult.Matched,
                matchMode: "semantic",
                selectedResponseId: selectedCandidate.ResponseId,
                selectedResponseStatusCode: selectedCandidate.ResponseStatusCode,
                selectionReason: "Semantic fallback selected the highest-scoring eligible candidate.");
        }

        var defaultResponse = GetDefaultResponse(operation, scenarioSnapshots);

        if (defaultResponse is not null)
        {
            return CreateSuccessExplanation(
                request,
                routeId,
                method,
                pathPattern,
                deterministicEvaluations,
                semanticEvaluationInfo,
                matched: true,
                matchResult: StubMatchResult.Matched,
                matchMode: "fallback",
                selectedResponseId: defaultResponse.Value.ResponseId,
                selectedResponseStatusCode: defaultResponse.Value.StatusCode,
                selectionReason: "No conditional candidate matched, so the eligible default response was selected.");
        }

        return CreateSuccessExplanation(
            request,
            routeId,
            method,
            pathPattern,
            deterministicEvaluations,
            semanticEvaluationInfo,
            matched: false,
            matchResult: StubMatchResult.ResponseNotConfigured,
            matchMode: null,
            selectedResponseId: null,
            selectedResponseStatusCode: null,
            selectionReason: "The route matched, but no eligible conditional or default response is configured for the current request and scenario state.");
    }

    private static MatchExplanationInfo CreateFailureExplanation(
        bool pathMatched,
        bool methodMatched,
        StubMatchResult matchResult,
        string selectionReason)
    {
        return new MatchExplanationInfo
        {
            PathMatched = pathMatched,
            MethodMatched = methodMatched,
            SelectionReason = selectionReason,
            Result = new MatchSimulationInfo
            {
                Matched = false,
                MatchResult = matchResult.ToString(),
            }
        };
    }

    private static MatchExplanationInfo CreateSuccessExplanation(
        MatchRequestInfo request,
        string routeId,
        string method,
        string pathPattern,
        IReadOnlyList<MatchCandidateInfo> deterministicCandidates,
        SemanticMatchInfo? semanticEvaluation,
        bool matched,
        StubMatchResult matchResult,
        string? matchMode,
        string? selectedResponseId,
        int? selectedResponseStatusCode,
        string selectionReason)
    {
        return new MatchExplanationInfo
        {
            PathMatched = true,
            MethodMatched = true,
            SelectionReason = selectionReason,
            DeterministicCandidates = deterministicCandidates,
            SemanticEvaluation = semanticEvaluation,
            Result = new MatchSimulationInfo
            {
                Matched = matched,
                MatchResult = matchResult.ToString(),
                RouteId = routeId,
                Method = method,
                PathPattern = pathPattern,
                SelectedResponseId = selectedResponseId,
                SelectedResponseStatusCode = selectedResponseStatusCode,
                MatchMode = matchMode,
                Candidates = request.IncludeCandidates ? deterministicCandidates : [],
            }
        };
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
            SemanticMatch = evaluation.Candidate.SemanticMatch,
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
                        SemanticMatch = score.Candidate.SemanticMatch ?? string.Empty,
                        Eligible = score.Eligible,
                        Score = score.Score,
                        AboveThreshold = score.AboveThreshold,
                    })
                    .ToList()
                : [],
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

        return scenarioService.GetSnapshots(scenarioNames);
    }

    private static IReadOnlyDictionary<string, StringValues> ConvertQueryValues(IReadOnlyDictionary<string, string[]> query)
    {
        return query.ToDictionary(
            entry => entry.Key,
            entry => new StringValues(entry.Value ?? Array.Empty<string>()),
            StringComparer.Ordinal);
    }

    private static string NormalizeMethod(string method)
        => string.IsNullOrWhiteSpace(method) ? HttpMethods.Get : method.Trim().ToUpperInvariant();

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        return path.StartsWith('/')
            ? path
            : "/" + path;
    }

    private static (string PathPattern, PathItemDefinition PathItem)? ResolvePath(StubDocument document, string requestPath)
    {
        if (document.Paths.TryGetValue(requestPath, out var exactPathItem))
        {
            return (requestPath, exactPathItem);
        }

        return document.Paths
            .Where(entry => IsTemplateMatch(entry.Key, requestPath))
            .OrderByDescending(entry => GetTemplateSpecificity(entry.Key))
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => ((string PathPattern, PathItemDefinition PathItem)?) (entry.Key, entry.Value))
            .FirstOrDefault();
    }

    private static bool IsTemplateMatch(string templatePath, string requestPath)
    {
        var templateSegments = GetPathSegments(templatePath);
        var requestSegments = GetPathSegments(requestPath);

        if (templateSegments.Length != requestSegments.Length)
        {
            return false;
        }

        for (var index = 0; index < templateSegments.Length; index++)
        {
            var templateSegment = templateSegments[index];
            var requestSegment = requestSegments[index];

            if (IsPathParameterSegment(templateSegment))
            {
                if (string.IsNullOrEmpty(requestSegment))
                {
                    return false;
                }

                continue;
            }

            if (!string.Equals(templateSegment, requestSegment, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static int GetTemplateSpecificity(string templatePath)
        => GetPathSegments(templatePath).Count(segment => !IsPathParameterSegment(segment));

    private static string[] GetPathSegments(string path)
        => path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static bool IsPathParameterSegment(string segment)
    {
        return segment.Length > 2 &&
               segment[0] == '{' &&
               segment[^1] == '}' &&
               !segment[1..^1].Contains('{') &&
               !segment[1..^1].Contains('}');
    }

    private static OperationDefinition? GetOperation(string method, PathItemDefinition pathItem)
    {
        if (HttpMethods.IsGet(method))
        {
            return pathItem.Get;
        }

        if (HttpMethods.IsPost(method))
        {
            return pathItem.Post;
        }

        if (HttpMethods.IsPut(method))
        {
            return pathItem.Put;
        }

        if (HttpMethods.IsPatch(method))
        {
            return pathItem.Patch;
        }

        if (HttpMethods.IsDelete(method))
        {
            return pathItem.Delete;
        }

        return null;
    }

    private static string GetRouteId(string method, string pathPattern, OperationDefinition operation)
    {
        return string.IsNullOrEmpty(operation.OperationId)
            ? $"{method}:{pathPattern}"
            : operation.OperationId;
    }

    private static bool IsDeterministicCandidate(QueryMatchDefinition candidate)
    {
        return candidate.Query.Count > 0 ||
               candidate.PartialQuery.Count > 0 ||
               candidate.RegexQuery.Count > 0 ||
               candidate.Headers.Count > 0 ||
               candidate.Body is not null;
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

    private static bool HasSemanticMatch(OperationDefinition op)
        => op.Matches.Any(m => m.SemanticMatch is not null);

    private static bool HasScenario(OperationDefinition op)
        => op.Responses.Values.Any(r => r.Scenario is not null)
        || op.Matches.Any(m => m.Response.Scenario is not null);

    private static IReadOnlyList<string> GetScenarioNames(StubDocument document)
    {
        var scenarioNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pathItem in document.Paths.Values)
        {
            AddScenarioNames(pathItem.Get, scenarioNames);
            AddScenarioNames(pathItem.Post, scenarioNames);
            AddScenarioNames(pathItem.Put, scenarioNames);
            AddScenarioNames(pathItem.Patch, scenarioNames);
            AddScenarioNames(pathItem.Delete, scenarioNames);
        }

        return scenarioNames.OrderBy(name => name, StringComparer.Ordinal).ToList();
    }

    private static void AddScenarioNames(OperationDefinition? operation, ISet<string> scenarioNames)
    {
        if (operation is null)
        {
            return;
        }

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
    }

    private static string ComputeDocumentHash(StubDocument document)
    {
        // Serialize a stable, ordered summary of the full route configuration.
        // Includes operation-level details (operationId, response keys, match rules,
        // semantic match descriptions) so that changes within existing routes are reflected
        // in the hash, not just path/method presence changes.
        // Avoids object?-typed fields (query dicts, body) to prevent serialization issues.
        var summary = document.Paths
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new
            {
                Path = p.Key,
                Operations = GetOperationSummaries(p.Value),
            });

        var json = JsonSerializer.Serialize(summary);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static IEnumerable<object> GetOperationSummaries(PathItemDefinition pathItem)
    {
        var methods = new (string Method, OperationDefinition? Op)[]
        {
            ("GET", pathItem.Get),
            ("POST", pathItem.Post),
            ("PUT", pathItem.Put),
            ("PATCH", pathItem.Patch),
            ("DELETE", pathItem.Delete),
        };

        foreach (var (method, op) in methods)
        {
            if (op is null) continue;

            yield return new
            {
                Method = method,
                OperationId = op.OperationId,
                Responses = op.Responses.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                MatchCount = op.Matches.Count,
                SemanticMatches = op.Matches
                    .Where(m => m.SemanticMatch is not null)
                    .Select(m => m.SemanticMatch!)
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToList(),
            };
        }
    }
}
