using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Models;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services;
using SemanticStub.Application.Services.Semantic;

namespace SemanticStub.Api.Services;

internal sealed class StubInspectionProjectionBuilder
{
    private readonly ScenarioService _scenarioService;

    public StubInspectionProjectionBuilder(ScenarioService scenarioService)
    {
        _scenarioService = scenarioService;
    }

    public MatchRequestInfo CreateInspectionRequest(
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

    public IReadOnlyDictionary<string, ScenarioStateSnapshot> GetScenarioSnapshots(OperationDefinition operation)
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
            scenarioName => _scenarioService.GetSnapshotWithinLock(scenarioName),
            StringComparer.Ordinal);
    }

    public MatchCandidateInfo CreateCandidateInfo(
        QueryMatchCandidateEvaluation evaluation,
        int index,
        IReadOnlyDictionary<string, ScenarioStateSnapshot> scenarioSnapshots)
    {
        int? responseStatusCode = evaluation.Candidate.Response.StatusCode > 0
            ? evaluation.Candidate.Response.StatusCode
            : null;
        var scenarioMatched = IsScenarioMatch(evaluation.Candidate.Response.Scenario, scenarioSnapshots);
        var responseConfigured = IsConfiguredResponse(evaluation.Candidate.Response);
        var matched = evaluation.Matched && scenarioMatched;

        var mismatchReasons = BuildMismatchReasons(evaluation, scenarioMatched, responseConfigured, matched, scenarioSnapshots);

        return new MatchCandidateInfo
        {
            CandidateIndex = index,
            QueryMatched = evaluation.QueryMatched,
            HeaderMatched = evaluation.HeaderMatched,
            BodyMatched = evaluation.BodyMatched,
            ScenarioMatched = scenarioMatched,
            ResponseConfigured = responseConfigured,
            Matched = matched,
            ResponseId = responseStatusCode?.ToString(),
            ResponseStatusCode = responseStatusCode,
            MismatchReasons = mismatchReasons,
        };
    }

    private static IReadOnlyList<MatchDimensionMismatchInfo> BuildMismatchReasons(
        QueryMatchCandidateEvaluation evaluation,
        bool scenarioMatched,
        bool responseConfigured,
        bool matched,
        IReadOnlyDictionary<string, ScenarioStateSnapshot> scenarioSnapshots)
    {
        if (matched && responseConfigured)
        {
            return [];
        }

        var reasons = new List<MatchDimensionMismatchInfo>();

        foreach (var mismatch in evaluation.MismatchReasons)
        {
            reasons.Add(new MatchDimensionMismatchInfo
            {
                Dimension = mismatch.Dimension,
                Key = mismatch.Key,
                Expected = mismatch.Expected,
                Actual = mismatch.Actual,
                Kind = mismatch.Kind,
            });
        }

        if (!scenarioMatched && evaluation.Candidate.Response.Scenario is { } scenario)
        {
            var currentState = scenarioSnapshots.TryGetValue(scenario.Name, out var snapshot)
                ? snapshot.State
                : "initial";

            reasons.Add(new MatchDimensionMismatchInfo
            {
                Dimension = "scenario",
                Key = scenario.Name,
                Expected = scenario.State,
                Actual = currentState,
                Kind = string.Equals(currentState, "initial", StringComparison.Ordinal) && !string.Equals(scenario.State, "initial", StringComparison.Ordinal)
                    ? "missing"
                    : "unequal",
            });
        }

        if (!responseConfigured && evaluation.Matched && scenarioMatched)
        {
            reasons.Add(new MatchDimensionMismatchInfo
            {
                Dimension = "response",
                Key = null,
                Expected = null,
                Actual = null,
                Kind = "notConfigured",
            });
        }

        return reasons;
    }

    public SemanticMatchInfo CreateSemanticMatchInfo(
        SemanticMatchExplanation explanation,
        OperationDefinition operation,
        bool includeCandidates)
    {
        return new SemanticMatchInfo
        {
            Attempted = explanation.Attempted,
            SelectionStatus = explanation.SelectionStatus,
            NonSelectionReason = explanation.NonSelectionReason,
            Threshold = explanation.Threshold,
            RequiredMargin = explanation.RequiredMargin,
            SelectedScore = explanation.SelectedScore,
            SecondBestScore = explanation.SecondBestScore,
            MarginToSecondBest = explanation.MarginToSecondBest,
            BestCandidateIndex = GetCandidateIndex(operation, explanation.BestCandidate),
            BestScore = explanation.BestScore,
            SecondBestCandidateIndex = GetCandidateIndex(operation, explanation.SecondBestCandidate),
            Candidates = includeCandidates
                ? explanation.CandidateScores
                    .Select(score => new SemanticCandidateInfo
                    {
                        CandidateIndex = GetCandidateIndex(operation, score.Candidate) ?? -1,
                        Eligible = score.Eligible,
                        Score = score.Score,
                        AboveThreshold = score.AboveThreshold,
                    })
                    .ToList()
                : [],
        };
    }

    private static int? GetCandidateIndex(OperationDefinition operation, QueryMatchDefinition? candidate)
    {
        if (candidate is null)
        {
            return null;
        }

        var index = operation.Matches.FindIndex(match => ReferenceEquals(match, candidate));
        return index >= 0 ? index : null;
    }

    public static bool IsConfiguredResponse(QueryMatchResponseDefinition response)
    {
        return response.StatusCode > 0 &&
               (!string.IsNullOrEmpty(response.ResponseFile) || response.Content.Count > 0);
    }

    public static bool IsConfiguredResponse(ResponseDefinition response)
    {
        return !string.IsNullOrEmpty(response.ResponseFile) || response.Content.Count > 0;
    }

    public static bool IsScenarioMatch(
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
}
