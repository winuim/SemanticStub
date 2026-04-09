using SemanticStub.Api.Inspection;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class StubMatchExplanationBuilderTests
{
    [Fact]
    public void CreateFailedDispatchResult_PreservesFailureExplanationShape()
    {
        var dispatch = StubMatchExplanationBuilder.CreateFailedDispatchResult(
            StubMatchResult.MethodNotAllowed,
            "The request path matched, but the HTTP method is not configured for that route.",
            pathMatched: true,
            methodMatched: false);

        Assert.Equal(StubMatchResult.MethodNotAllowed, dispatch.Result);
        Assert.True(dispatch.Explanation.PathMatched);
        Assert.False(dispatch.Explanation.MethodMatched);
        Assert.Equal("The request path matched, but the HTTP method is not configured for that route.", dispatch.Explanation.SelectionReason);
        Assert.False(dispatch.Explanation.Result.Matched);
        Assert.Equal("MethodNotAllowed", dispatch.Explanation.Result.MatchResult);
    }

    [Fact]
    public void CreateMatchedDispatchResult_HidesCandidatesFromSimulationResultWhenNotRequested()
    {
        var request = new MatchRequestInfo
        {
            Method = "GET",
            Path = "/users",
            IncludeCandidates = false,
        };
        var deterministicCandidates = new List<MatchCandidateInfo>
        {
            new()
            {
                CandidateIndex = 0,
                Matched = true,
                ResponseConfigured = true
            }
        };

        var dispatch = StubMatchExplanationBuilder.CreateMatchedDispatchResult(
            request,
            "listUsers",
            "GET",
            "/users",
            deterministicCandidates,
            semanticEvaluation: null,
            StubMatchResult.Matched,
            new StubResponse { StatusCode = 200, Body = "{}", ContentType = "application/json" },
            "fallback",
            "200",
            200,
            "No conditional candidate matched, so the eligible default response was selected.");

        Assert.True(dispatch.Explanation.PathMatched);
        Assert.True(dispatch.Explanation.MethodMatched);
        Assert.Single(dispatch.Explanation.DeterministicCandidates);
        Assert.Empty(dispatch.Explanation.Result.Candidates);
        Assert.Equal("listUsers", dispatch.Explanation.Result.RouteId);
        Assert.Equal("fallback", dispatch.Explanation.Result.MatchMode);
    }
}
