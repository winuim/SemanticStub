using SemanticStub.Api.Inspection;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

internal static class StubMatchExplanationBuilder
{
    public static StubDispatchResult CreateFailedDispatchResult(
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

    public static StubDispatchResult CreateMatchedDispatchResult(
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
}
