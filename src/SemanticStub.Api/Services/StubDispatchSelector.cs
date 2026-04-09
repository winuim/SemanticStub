using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

internal sealed class StubDispatchSelector
{
    private readonly StubDefaultResponseSelector defaultResponseSelector;
    private readonly IMatcherService matcherService;
    private readonly ISemanticMatcherService? semanticMatcherService;
    private readonly StubResponseBuilder responseBuilder;
    private readonly ScenarioService scenarioService;
    private readonly ILogger? logger;

    public StubDispatchSelector(
        IMatcherService matcherService,
        ISemanticMatcherService? semanticMatcherService,
        StubResponseBuilder responseBuilder,
        ScenarioService scenarioService,
        ILogger? logger)
    {
        this.matcherService = matcherService;
        this.semanticMatcherService = semanticMatcherService;
        this.responseBuilder = responseBuilder;
        this.scenarioService = scenarioService;
        this.logger = logger;
        defaultResponseSelector = new StubDefaultResponseSelector(responseBuilder, scenarioService);
    }

    public async Task<StubDispatchSelectionResult> SelectAsync(
        string method,
        string path,
        PathItemDefinition pathItem,
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        bool mutateScenarioState,
        bool includeSemanticCandidates)
    {
        var selectedDeterministicCandidate = matcherService.FindBestMatch(
            pathItem.Parameters,
            operation,
            query,
            headers,
            body,
            candidate => scenarioService.IsMatch(candidate.Response.Scenario) && IsDeterministicCandidate(candidate));

        if (selectedDeterministicCandidate is not null)
        {
            var matchedCandidateIndex = operation.Matches.FindIndex(candidate => ReferenceEquals(candidate, selectedDeterministicCandidate));

            if (!responseBuilder.TryBuild(selectedDeterministicCandidate.Response, out var deterministicResponse))
            {
                return new StubDispatchSelectionResult
                {
                    Result = StubMatchResult.ResponseNotConfigured,
                    MatchMode = "exact",
                    SelectedCandidate = selectedDeterministicCandidate,
                    SelectedResponseId = selectedDeterministicCandidate.Response.StatusCode.ToString(),
                    SelectedResponseStatusCode = selectedDeterministicCandidate.Response.StatusCode,
                    SelectionReason = $"Deterministic candidate {matchedCandidateIndex} matched, but its response is not configured.",
                };
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

            return new StubDispatchSelectionResult
            {
                Result = StubMatchResult.Matched,
                Response = deterministicResponse,
                MatchMode = "exact",
                SelectedCandidate = selectedDeterministicCandidate,
                SelectedResponseId = selectedDeterministicCandidate.Response.StatusCode.ToString(),
                SelectedResponseStatusCode = selectedDeterministicCandidate.Response.StatusCode,
                SelectionReason = $"Deterministic candidate {matchedCandidateIndex} matched all configured conditions and was selected.",
            };
        }

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

        if (semanticExplanation.SelectedCandidate is not null)
        {
            if (!responseBuilder.TryBuild(semanticExplanation.SelectedCandidate.Response, out var semanticResponse))
            {
                return new StubDispatchSelectionResult
                {
                    Result = StubMatchResult.ResponseNotConfigured,
                    Response = null,
                    MatchMode = "semantic",
                    SelectedCandidate = semanticExplanation.SelectedCandidate,
                    SelectedResponseId = semanticExplanation.SelectedCandidate.Response.StatusCode.ToString(),
                    SelectedResponseStatusCode = semanticExplanation.SelectedCandidate.Response.StatusCode,
                    SelectionReason = "Semantic fallback selected a candidate, but its response is not configured.",
                    SemanticExplanation = semanticExplanation,
                };
            }

            logger?.LogInformation(
                "Semantic fallback produced a match for '{Path}' {Method}.",
                path,
                method.ToUpperInvariant());

            if (mutateScenarioState)
            {
                scenarioService.Advance(semanticExplanation.SelectedCandidate.Response.Scenario);
            }

            return new StubDispatchSelectionResult
            {
                Result = StubMatchResult.Matched,
                Response = semanticResponse,
                MatchMode = "semantic",
                SelectedCandidate = semanticExplanation.SelectedCandidate,
                SelectedResponseId = semanticExplanation.SelectedCandidate.Response.StatusCode.ToString(),
                SelectedResponseStatusCode = semanticExplanation.SelectedCandidate.Response.StatusCode,
                SelectionReason = "Semantic fallback selected the highest-scoring eligible candidate.",
                SemanticExplanation = semanticExplanation,
            };
        }

        if (defaultResponseSelector.TrySelect(operation, mutateScenarioState, out var defaultSelection))
        {
            return new StubDispatchSelectionResult
            {
                Result = StubMatchResult.Matched,
                Response = defaultSelection.Response,
                MatchMode = "fallback",
                SelectedResponseId = defaultSelection.ResponseId,
                SelectedResponseStatusCode = defaultSelection.StatusCode,
                SelectionReason = "No conditional candidate matched, so the eligible default response was selected.",
                SemanticExplanation = semanticExplanation,
            };
        }

        return new StubDispatchSelectionResult
        {
            Result = StubMatchResult.ResponseNotConfigured,
            Response = null,
            MatchMode = null,
            SelectedResponseId = null,
            SelectedResponseStatusCode = null,
            SelectionReason = "The route matched, but no eligible conditional or default response is configured for the current request and scenario state.",
            SemanticExplanation = semanticExplanation,
        };
    }

    private static bool IsDeterministicCandidate(QueryMatchDefinition candidate)
    {
        return candidate.Query.Count > 0 ||
               candidate.PartialQuery.Count > 0 ||
               candidate.RegexQuery.Count > 0 ||
               candidate.Headers.Count > 0 ||
               candidate.Body is not null;
    }
}

internal sealed class StubDispatchSelectionResult
{
    public StubMatchResult Result { get; init; }

    public StubResponse? Response { get; init; }

    public string? MatchMode { get; init; }

    public QueryMatchDefinition? SelectedCandidate { get; init; }

    public string? SelectedResponseId { get; init; }

    public int? SelectedResponseStatusCode { get; init; }

    public string SelectionReason { get; init; } = string.Empty;

    public SemanticMatchExplanation SemanticExplanation { get; init; } = new();
}
