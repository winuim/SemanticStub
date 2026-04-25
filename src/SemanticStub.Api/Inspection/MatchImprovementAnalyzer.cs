namespace SemanticStub.Api.Inspection;

/// <summary>
/// Analyzes a match explanation and produces actionable suggestions for improving stub definitions.
/// </summary>
public static class MatchImprovementAnalyzer
{
    /// <summary>
    /// Analyzes the supplied explanation and returns a report containing improvement suggestions.
    /// </summary>
    /// <param name="explanation">The match explanation to analyze.</param>
    /// <returns>
    /// A <see cref="MatchImprovementReportInfo"/> whose <c>Suggestions</c> list is empty
    /// when no issues are detected.
    /// </returns>
    public static MatchImprovementReportInfo Analyze(MatchExplanationInfo explanation)
    {
        ArgumentNullException.ThrowIfNull(explanation);

        var suggestions = new List<MatchImprovementSuggestionInfo>();

        if (!explanation.PathMatched)
        {
            suggestions.Add(new MatchImprovementSuggestionInfo
            {
                Kind = "NoMatchFound",
                Reason = "No stub is defined for this request path.",
                YamlHint = "Add a new path entry under 'paths:' in your stub YAML for this endpoint.",
            });
            return new MatchImprovementReportInfo { Explanation = explanation, Suggestions = suggestions };
        }

        if (!explanation.MethodMatched)
        {
            suggestions.Add(new MatchImprovementSuggestionInfo
            {
                Kind = "NoMatchFound",
                Reason = "The request path is defined but the HTTP method is not.",
                YamlHint = "Add the missing HTTP method under the path entry in your stub YAML.",
            });
            return new MatchImprovementReportInfo { Explanation = explanation, Suggestions = suggestions };
        }

        if (IsSemanticMatch(explanation))
        {
            suggestions.Add(new MatchImprovementSuggestionInfo
            {
                Kind = "SemanticFallbackUsed",
                Reason = "The request was matched using semantic (vector) similarity because no deterministic x-match condition matched.",
                YamlHint = "Add explicit 'x-match' conditions (query, headers, or body fields) to make this match deterministic and predictable.",
            });
        }

        if (IsNoConditionsRoute(explanation))
        {
            suggestions.Add(new MatchImprovementSuggestionInfo
            {
                Kind = "NoConditionsOnRoute",
                Reason = "No 'x-match' conditions are defined for this route — any request to this path and method will match.",
                YamlHint = "Add 'x-match' entries with query, header, or body conditions to distinguish different request scenarios.",
            });
        }

        foreach (var candidate in explanation.DeterministicCandidates)
        {
            if (candidate.Matched)
            {
                continue;
            }

            var failedMatchDimensions = candidate.MismatchReasons
                .Select(m => m.Dimension)
                .Where(d => d is not "response")
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (failedMatchDimensions.Count != 1)
            {
                continue;
            }

            var dimension = failedMatchDimensions[0];

            // scenario failures are not correctable via x-match conditions
            if (dimension is "scenario")
            {
                continue;
            }
            var firstMismatch = candidate.MismatchReasons.FirstOrDefault(m => m.Dimension == dimension);

            var keyDetail = firstMismatch?.Key is { Length: > 0 } key ? $" (key: '{key}')" : string.Empty;

            suggestions.Add(new MatchImprovementSuggestionInfo
            {
                Kind = "NearMissCandidate",
                CandidateIndex = candidate.CandidateIndex,
                Dimension = dimension,
                Reason = $"Candidate {candidate.CandidateIndex} nearly matched — only '{dimension}' did not pass{keyDetail}.",
                YamlHint = $"Review the '{dimension}' condition in x-match[{candidate.CandidateIndex}]. " +
                           "Consider whether the condition is too strict or whether the incoming request needs adjustment.",
            });
        }

        return new MatchImprovementReportInfo { Explanation = explanation, Suggestions = suggestions };
    }

    private static bool IsSemanticMatch(MatchExplanationInfo explanation) =>
        string.Equals(explanation.Result.MatchMode, "semantic", StringComparison.Ordinal);

    private static bool IsNoConditionsRoute(MatchExplanationInfo explanation) =>
        explanation.DeterministicCandidates.Count == 0 &&
        string.Equals(explanation.Result.MatchMode, "fallback", StringComparison.Ordinal);
}
