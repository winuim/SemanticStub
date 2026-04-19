using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Inspection;

public sealed class StubInspectionProjectionBuilderTests
{
    [Fact]
    public void CreateInspectionRequest_PreservesHeadersAndQueryValues()
    {
        var builder = new StubInspectionProjectionBuilder(new ScenarioService());
        var query = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["role"] = new(["admin", "editor"])
        };
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Env"] = "staging"
        };

        var request = builder.CreateInspectionRequest("GET", "/users", query, headers, "{}", includeCandidates: true, includeSemanticCandidates: true);

        Assert.Equal("GET", request.Method);
        Assert.Equal("/users", request.Path);
        Assert.Equal(["admin", "editor"], request.Query["role"]);
        Assert.Equal("staging", request.Headers["X-Env"]);
        Assert.True(request.IncludeCandidates);
        Assert.True(request.IncludeSemanticCandidates);
    }

    [Fact]
    public void CreateCandidateInfo_UsesScenarioSnapshotForMatchedFlag()
    {
        var builder = new StubInspectionProjectionBuilder(new ScenarioService());
        var candidate = new QueryMatchDefinition
        {
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 202,
                Scenario = new ScenarioDefinition
                {
                    Name = "checkout",
                    State = "pending"
                },
                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                {
                    ["application/json"] = new() { Example = new Dictionary<object, object> { ["result"] = "ok" } }
                }
            }
        };
        var evaluation = new QueryMatchCandidateEvaluation
        {
            Candidate = candidate,
            QueryMatched = true,
            HeaderMatched = true,
            BodyMatched = true,
        };
        var snapshots = new Dictionary<string, ScenarioStateSnapshot>(StringComparer.Ordinal)
        {
            ["checkout"] = new("pending", DateTimeOffset.UtcNow)
        };

        var info = builder.CreateCandidateInfo(evaluation, 0, snapshots);

        Assert.True(info.ScenarioMatched);
        Assert.True(info.Matched);
        Assert.True(info.ResponseConfigured);
        Assert.Equal("202", info.ResponseId);
        Assert.Equal(202, info.ResponseStatusCode);
    }

    [Fact]
    public void CreateSemanticMatchInfo_ProjectsCandidateIndexesWhenRequested()
    {
        var builder = new StubInspectionProjectionBuilder(new ScenarioService());
        var firstCandidate = new QueryMatchDefinition { Response = new QueryMatchResponseDefinition { StatusCode = 200 } };
        var secondCandidate = new QueryMatchDefinition { Response = new QueryMatchResponseDefinition { StatusCode = 201 } };
        var operation = new OperationDefinition
        {
            Matches = [firstCandidate, secondCandidate]
        };
        var explanation = new SemanticMatchExplanation
        {
            Attempted = true,
            SelectedScore = 0.97d,
            Threshold = 0.8d,
            CandidateScores =
            [
                new SemanticCandidateScore
                {
                    Candidate = secondCandidate,
                    Eligible = true,
                    Score = 0.97d,
                    AboveThreshold = true
                }
            ]
        };

        var info = builder.CreateSemanticMatchInfo(explanation, operation, includeCandidates: true);

        Assert.True(info.Attempted);
        Assert.Single(info.Candidates);
        Assert.Equal(1, info.Candidates[0].CandidateIndex);
        Assert.True(info.Candidates[0].Eligible);
        Assert.Equal(0.97d, info.Candidates[0].Score);
    }
}
