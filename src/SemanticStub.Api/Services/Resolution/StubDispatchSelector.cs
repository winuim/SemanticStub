using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services;
using SemanticStub.Application.Services.Semantic;

namespace SemanticStub.Api.Services;

internal sealed class StubDispatchSelector
{
    private readonly StubDefaultResponseSelector _defaultResponseSelector;
    private readonly MatcherService _matcherService;
    private readonly ISemanticMatcherService? _semanticMatcherService;
    private readonly StubResponseBuilder _responseBuilder;
    private readonly ScenarioService _scenarioService;
    private readonly ILogger? _logger;

    public StubDispatchSelector(
        MatcherService matcherService,
        ISemanticMatcherService? semanticMatcherService,
        StubResponseBuilder responseBuilder,
        StubDefaultResponseSelector defaultResponseSelector,
        ScenarioService scenarioService,
        ILogger? logger)
    {
        _matcherService = matcherService;
        _semanticMatcherService = semanticMatcherService;
        _responseBuilder = responseBuilder;
        _defaultResponseSelector = defaultResponseSelector;
        _scenarioService = scenarioService;
        _logger = logger;
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
        bool includeSemanticCandidates,
        CancellationToken cancellationToken = default)
    {
        var selectedDeterministicCandidate = _matcherService.FindBestMatch(
            pathItem.Parameters,
            operation,
            query,
            headers,
            body,
            candidate => _scenarioService.IsMatch(candidate.Response.Scenario) && IsDeterministicCandidate(candidate));

        if (selectedDeterministicCandidate is not null)
        {
            var matchedCandidateIndex = operation.Matches.FindIndex(candidate => ReferenceEquals(candidate, selectedDeterministicCandidate));

            if (!_responseBuilder.TryBuild(selectedDeterministicCandidate.Response, out var deterministicResponse))
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

            _logger?.LogInformation(
                "Deterministic conditional match selected for '{Path}' {Method}. QueryKeys={QueryKeys}, HeaderKeys={HeaderKeys}, HasBody={HasBody}.",
                path,
                method.ToUpperInvariant(),
                selectedDeterministicCandidate.Query.Count,
                selectedDeterministicCandidate.Headers.Count,
                selectedDeterministicCandidate.Body is not null);

            if (mutateScenarioState)
            {
                _scenarioService.Advance(selectedDeterministicCandidate.Response.Scenario);
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

        var semanticExplanation = _semanticMatcherService is null
            ? new SemanticMatchExplanation()
            : await _semanticMatcherService.ExplainMatchAsync(
                method,
                path,
                query,
                headers,
                body,
                operation.Matches,
                candidate => _scenarioService.IsMatch(candidate.Response.Scenario),
                includeCandidateScores: includeSemanticCandidates,
                cancellationToken: cancellationToken);

        if (semanticExplanation.SelectedCandidate is not null)
        {
            if (!_responseBuilder.TryBuild(semanticExplanation.SelectedCandidate.Response, out var semanticResponse))
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

            _logger?.LogInformation(
                "Semantic fallback produced a match for '{Path}' {Method}.",
                path,
                method.ToUpperInvariant());

            if (mutateScenarioState)
            {
                _scenarioService.Advance(semanticExplanation.SelectedCandidate.Response.Scenario);
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

        if (_defaultResponseSelector.TrySelect(operation, mutateScenarioState, out var defaultSelection))
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
