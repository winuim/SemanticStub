using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Models;

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

    public SemanticMatchInfo CreateSemanticMatchInfo(
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
