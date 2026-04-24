using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Models;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services;
using SemanticStub.Application.Services.Semantic;
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
    public void CreateCandidateInfo_QueryMismatch_PropagatesMismatchReasons()
    {
        var builder = new StubInspectionProjectionBuilder(new ScenarioService());
        var candidate = new QueryMatchDefinition
        {
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 200,
                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                {
                    ["application/json"] = new() { Example = new Dictionary<object, object> { ["ok"] = true } }
                }
            }
        };
        var evaluation = new QueryMatchCandidateEvaluation
        {
            Candidate = candidate,
            QueryMatched = false,
            HeaderMatched = true,
            BodyMatched = true,
            MismatchReasons =
            [
                new SemanticStub.Application.Services.MatchDimensionMismatch
                {
                    Dimension = "query",
                    Key = "role",
                    Expected = "admin",
                    Actual = "user",
                    Kind = "unequal",
                }
            ]
        };

        var info = builder.CreateCandidateInfo(evaluation, 0, new Dictionary<string, ScenarioStateSnapshot>());

        Assert.False(info.Matched);
        var mismatch = Assert.Single(info.MismatchReasons);
        Assert.Equal("query", mismatch.Dimension);
        Assert.Equal("role", mismatch.Key);
        Assert.Equal("admin", mismatch.Expected);
        Assert.Equal("user", mismatch.Actual);
        Assert.Equal("unequal", mismatch.Kind);
    }

    [Fact]
    public void CreateCandidateInfo_ScenarioMismatch_EmitsScenarioDimensionEntry()
    {
        var builder = new StubInspectionProjectionBuilder(new ScenarioService());
        var candidate = new QueryMatchDefinition
        {
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 202,
                Scenario = new ScenarioDefinition { Name = "billing", State = "ready" },
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
            ["billing"] = new("initial", DateTimeOffset.UtcNow)
        };

        var info = builder.CreateCandidateInfo(evaluation, 0, snapshots);

        Assert.False(info.ScenarioMatched);
        Assert.False(info.Matched);
        var mismatch = Assert.Single(info.MismatchReasons);
        Assert.Equal("scenario", mismatch.Dimension);
        Assert.Equal("billing", mismatch.Key);
        Assert.Equal("ready", mismatch.Expected);
        Assert.Equal("initial", mismatch.Actual);
        Assert.Equal("missing", mismatch.Kind);
    }

    [Fact]
    public void CreateCandidateInfo_ScenarioStateWrongValue_EmitsUnequalKind()
    {
        var builder = new StubInspectionProjectionBuilder(new ScenarioService());
        var candidate = new QueryMatchDefinition
        {
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 202,
                Scenario = new ScenarioDefinition { Name = "billing", State = "ready" },
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
            ["billing"] = new("pending", DateTimeOffset.UtcNow)
        };

        var info = builder.CreateCandidateInfo(evaluation, 0, snapshots);

        var mismatch = Assert.Single(info.MismatchReasons);
        Assert.Equal("scenario", mismatch.Dimension);
        Assert.Equal("pending", mismatch.Actual);
        Assert.Equal("unequal", mismatch.Kind);
    }

    [Fact]
    public void CreateCandidateInfo_ResponseNotConfigured_EmitsResponseDimensionEntry()
    {
        var builder = new StubInspectionProjectionBuilder(new ScenarioService());
        var candidate = new QueryMatchDefinition
        {
            Query = new Dictionary<string, object?>(StringComparer.Ordinal) { ["role"] = "admin" },
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 0,
            }
        };
        var evaluation = new QueryMatchCandidateEvaluation
        {
            Candidate = candidate,
            QueryMatched = true,
            HeaderMatched = true,
            BodyMatched = true,
        };

        var info = builder.CreateCandidateInfo(evaluation, 0, new Dictionary<string, ScenarioStateSnapshot>());

        Assert.False(info.ResponseConfigured);
        var mismatch = Assert.Single(info.MismatchReasons);
        Assert.Equal("response", mismatch.Dimension);
        Assert.Null(mismatch.Key);
        Assert.Equal("notConfigured", mismatch.Kind);
    }

    [Fact]
    public void CreateCandidateInfo_FullMatch_EmptyMismatchReasons()
    {
        var builder = new StubInspectionProjectionBuilder(new ScenarioService());
        var candidate = new QueryMatchDefinition
        {
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 200,
                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                {
                    ["application/json"] = new() { Example = new Dictionary<object, object> { ["ok"] = true } }
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

        var info = builder.CreateCandidateInfo(evaluation, 0, new Dictionary<string, ScenarioStateSnapshot>());

        Assert.True(info.Matched);
        Assert.Empty(info.MismatchReasons);
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
            SelectedCandidate = secondCandidate,
            BestCandidate = secondCandidate,
            BestScore = 0.97d,
            SecondBestCandidate = firstCandidate,
            SelectedScore = 0.97d,
            SecondBestScore = 0.81d,
            MarginToSecondBest = 0.16d,
            Threshold = 0.8d,
            SelectionStatus = SemanticSelectionStatus.Selected,
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
        Assert.Equal("selected", info.SelectionStatus);
        Assert.Null(info.NonSelectionReason);
        Assert.Equal(1, info.BestCandidateIndex);
        Assert.Equal(0.97d, info.BestScore);
        Assert.Equal(0, info.SecondBestCandidateIndex);
        Assert.Single(info.Candidates);
        Assert.Equal(1, info.Candidates[0].CandidateIndex);
        Assert.True(info.Candidates[0].Eligible);
        Assert.Equal(0.97d, info.Candidates[0].Score);
    }

    [Fact]
    public void CreateSemanticMatchInfo_WhenScoreBelowThreshold_SetsBelowThresholdStatus()
    {
        var builder = new StubInspectionProjectionBuilder(new ScenarioService());
        var candidate = new QueryMatchDefinition { Response = new QueryMatchResponseDefinition { StatusCode = 200 } };
        var operation = new OperationDefinition
        {
            Matches = [candidate]
        };
        var explanation = new SemanticMatchExplanation
        {
            Attempted = true,
            BestCandidate = candidate,
            BestScore = 0.72d,
            Threshold = 0.8d,
            RequiredMargin = 0.03d,
            SelectionStatus = SemanticSelectionStatus.BelowThreshold,
            NonSelectionReason = SemanticSelectionStatus.BelowThreshold,
        };

        var info = builder.CreateSemanticMatchInfo(explanation, operation, includeCandidates: false);

        Assert.Equal("belowThreshold", info.SelectionStatus);
        Assert.Equal("belowThreshold", info.NonSelectionReason);
        Assert.Equal(0, info.BestCandidateIndex);
        Assert.Equal(0.72d, info.BestScore);
        Assert.Null(info.SelectedScore);
        Assert.Empty(info.Candidates);
    }

    [Fact]
    public void CreateSemanticMatchInfo_WhenOnlyOneCandidateIsAboveThreshold_LeavesSecondBestIndexNull()
    {
        var builder = new StubInspectionProjectionBuilder(new ScenarioService());
        var selectedCandidate = new QueryMatchDefinition { Response = new QueryMatchResponseDefinition { StatusCode = 200 } };
        var belowThresholdCandidate = new QueryMatchDefinition { Response = new QueryMatchResponseDefinition { StatusCode = 201 } };
        var operation = new OperationDefinition
        {
            Matches = [selectedCandidate, belowThresholdCandidate]
        };
        var explanation = new SemanticMatchExplanation
        {
            Attempted = true,
            SelectedCandidate = selectedCandidate,
            BestCandidate = selectedCandidate,
            BestScore = 0.95d,
            SelectedScore = 0.95d,
            Threshold = 0.8d,
            RequiredMargin = 0.03d,
            SelectionStatus = SemanticSelectionStatus.Selected,
        };

        var info = builder.CreateSemanticMatchInfo(explanation, operation, includeCandidates: false);

        Assert.Equal("selected", info.SelectionStatus);
        Assert.Equal(0, info.BestCandidateIndex);
        Assert.Null(info.SecondBestCandidateIndex);
        Assert.Null(info.SecondBestScore);
    }

    [Fact]
    public void CreateSemanticMatchInfo_WhenMarginTooSmall_SetsAmbiguousStatus()
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
            BestCandidate = firstCandidate,
            BestScore = 0.95d,
            SecondBestCandidate = secondCandidate,
            SelectedScore = 0.95d,
            SecondBestScore = 0.93d,
            MarginToSecondBest = 0.02d,
            Threshold = 0.8d,
            RequiredMargin = 0.03d,
            SelectionStatus = SemanticSelectionStatus.Ambiguous,
            NonSelectionReason = SemanticSelectionStatus.Ambiguous,
        };

        var info = builder.CreateSemanticMatchInfo(explanation, operation, includeCandidates: false);

        Assert.Equal("ambiguous", info.SelectionStatus);
        Assert.Equal("ambiguous", info.NonSelectionReason);
        Assert.Equal(0, info.BestCandidateIndex);
        Assert.Equal(1, info.SecondBestCandidateIndex);
        Assert.Equal(0.02d, info.MarginToSecondBest);
    }
}
