using SemanticStub.Api.Inspection;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Inspection;

public sealed class MatchImprovementAnalyzerTests
{
    private static MatchExplanationInfo MakeExplanation(
        bool pathMatched = true,
        bool methodMatched = true,
        string? matchMode = "exact",
        bool matched = true,
        IReadOnlyList<MatchCandidateInfo>? deterministicCandidates = null)
    {
        return new MatchExplanationInfo
        {
            PathMatched = pathMatched,
            MethodMatched = methodMatched,
            DeterministicCandidates = deterministicCandidates ?? [],
            SelectionReason = string.Empty,
            Result = new MatchSimulationInfo
            {
                Matched = matched,
                MatchResult = matched ? "Matched" : "NotMatched",
                MatchMode = matchMode,
            },
        };
    }

    private static MatchCandidateInfo MakeCandidate(
        int index = 0,
        bool matched = true,
        IReadOnlyList<MatchDimensionMismatchInfo>? mismatches = null)
    {
        return new MatchCandidateInfo
        {
            CandidateIndex = index,
            Matched = matched,
            QueryMatched = true,
            HeaderMatched = true,
            BodyMatched = true,
            ScenarioMatched = true,
            ResponseConfigured = true,
            MismatchReasons = mismatches ?? [],
        };
    }

    private static MatchDimensionMismatchInfo MakeMismatch(string dimension, string key = "field") =>
        new() { Dimension = dimension, Key = key, Expected = "expected", Actual = "actual", Kind = "unequal" };

    [Fact]
    public void Analyze_WhenCleanDeterministicMatch_ReturnsNoSuggestions()
    {
        var explanation = MakeExplanation(
            matchMode: "exact",
            deterministicCandidates: [MakeCandidate(matched: true)]);

        var report = MatchImprovementAnalyzer.Analyze(explanation);

        Assert.Empty(report.Suggestions);
    }

    [Fact]
    public void Analyze_WhenPathNotFound_ReturnsSingleNoMatchFoundSuggestion()
    {
        var explanation = MakeExplanation(pathMatched: false, methodMatched: false, matchMode: null, matched: false);

        var report = MatchImprovementAnalyzer.Analyze(explanation);

        Assert.Single(report.Suggestions);
        Assert.Equal("NoMatchFound", report.Suggestions[0].Kind);
        Assert.Contains("path", report.Suggestions[0].Reason);
    }

    [Fact]
    public void Analyze_WhenMethodNotFound_ReturnsSingleNoMatchFoundSuggestion()
    {
        var explanation = MakeExplanation(pathMatched: true, methodMatched: false, matchMode: null, matched: false);

        var report = MatchImprovementAnalyzer.Analyze(explanation);

        Assert.Single(report.Suggestions);
        Assert.Equal("NoMatchFound", report.Suggestions[0].Kind);
        Assert.Contains("method", report.Suggestions[0].Reason);
    }

    [Fact]
    public void Analyze_WhenSemanticFallback_SuggestsAddingExplicitConditions()
    {
        var explanation = MakeExplanation(matchMode: "semantic");

        var report = MatchImprovementAnalyzer.Analyze(explanation);

        var suggestion = Assert.Single(report.Suggestions, s => s.Kind == "SemanticFallbackUsed");
        Assert.Contains("x-match", suggestion.YamlHint);
    }

    [Fact]
    public void Analyze_WhenSemanticModeButResponseNotConfigured_DoesNotSuggestSemanticFallback()
    {
        var explanation = MakeExplanation(matchMode: "semantic", matched: false);

        var report = MatchImprovementAnalyzer.Analyze(explanation);

        Assert.DoesNotContain(report.Suggestions, s => s.Kind == "SemanticFallbackUsed");
    }

    [Fact]
    public void Analyze_WhenNoConditionsOnRoute_SuggestsAddingConditions()
    {
        var explanation = MakeExplanation(matchMode: "fallback", deterministicCandidates: []);

        var report = MatchImprovementAnalyzer.Analyze(explanation);

        var suggestion = Assert.Single(report.Suggestions, s => s.Kind == "NoConditionsOnRoute");
        Assert.Contains("x-match", suggestion.YamlHint);
    }

    [Fact]
    public void Analyze_WhenNearMissCandidate_SuggestsConditionRefinement()
    {
        var candidate = MakeCandidate(
            index: 1,
            matched: false,
            mismatches: [MakeMismatch("query", "status")]);

        var explanation = MakeExplanation(
            matchMode: null,
            matched: false,
            deterministicCandidates: [candidate]);

        var report = MatchImprovementAnalyzer.Analyze(explanation);

        var suggestion = Assert.Single(report.Suggestions, s => s.Kind == "NearMissCandidate");
        Assert.Equal(1, suggestion.CandidateIndex);
        Assert.Equal("query", suggestion.Dimension);
        Assert.Contains("status", suggestion.Reason);
        Assert.Contains("x-match[1]", suggestion.YamlHint);
    }

    [Fact]
    public void Analyze_WhenCandidateMissesOnMultipleDimensions_DoesNotSuggestNearMiss()
    {
        var candidate = MakeCandidate(
            index: 0,
            matched: false,
            mismatches:
            [
                MakeMismatch("query", "type"),
                MakeMismatch("body", "id"),
            ]);

        var explanation = MakeExplanation(
            matchMode: null,
            matched: false,
            deterministicCandidates: [candidate]);

        var report = MatchImprovementAnalyzer.Analyze(explanation);

        Assert.DoesNotContain(report.Suggestions, s => s.Kind == "NearMissCandidate");
    }

    [Fact]
    public void Analyze_WhenCandidateMatchedSuccessfully_DoesNotSuggestNearMiss()
    {
        var candidate = MakeCandidate(index: 0, matched: true);

        var explanation = MakeExplanation(
            matchMode: "exact",
            deterministicCandidates: [candidate]);

        var report = MatchImprovementAnalyzer.Analyze(explanation);

        Assert.DoesNotContain(report.Suggestions, s => s.Kind == "NearMissCandidate");
    }

    [Fact]
    public void Analyze_WhenCandidateMissesOnScenarioAndOneDimension_DoesNotSuggestNearMiss()
    {
        var candidate = MakeCandidate(
            index: 0,
            matched: false,
            mismatches:
            [
                MakeMismatch("scenario", "myScenario"),
                MakeMismatch("query", "status"),
            ]);

        var explanation = MakeExplanation(
            matchMode: null,
            matched: false,
            deterministicCandidates: [candidate]);

        var report = MatchImprovementAnalyzer.Analyze(explanation);

        Assert.DoesNotContain(report.Suggestions, s => s.Kind == "NearMissCandidate");
    }

    [Fact]
    public void Analyze_WhenCandidateMissesOnScenarioOnly_DoesNotSuggestNearMiss()
    {
        var candidate = MakeCandidate(
            index: 0,
            matched: false,
            mismatches: [MakeMismatch("scenario", "myScenario")]);

        var explanation = MakeExplanation(
            matchMode: null,
            matched: false,
            deterministicCandidates: [candidate]);

        var report = MatchImprovementAnalyzer.Analyze(explanation);

        Assert.DoesNotContain(report.Suggestions, s => s.Kind == "NearMissCandidate");
    }

    [Fact]
    public void Analyze_WhenResponseDimensionOnlyMismatch_DoesNotSuggestNearMiss()
    {
        var candidate = MakeCandidate(
            index: 0,
            matched: false,
            mismatches: [new MatchDimensionMismatchInfo { Dimension = "response", Kind = "notConfigured" }]);

        var explanation = MakeExplanation(
            matchMode: null,
            matched: false,
            deterministicCandidates: [candidate]);

        var report = MatchImprovementAnalyzer.Analyze(explanation);

        Assert.DoesNotContain(report.Suggestions, s => s.Kind == "NearMissCandidate");
    }

    [Fact]
    public void Analyze_ReturnsExplanationInReport()
    {
        var explanation = MakeExplanation();

        var report = MatchImprovementAnalyzer.Analyze(explanation);

        Assert.Same(explanation, report.Explanation);
    }

    [Fact]
    public void Analyze_ThrowsWhenExplanationIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => MatchImprovementAnalyzer.Analyze(null!));
    }
}
