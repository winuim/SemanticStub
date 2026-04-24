namespace SemanticStub.Application.Services.Semantic;

/// <summary>
/// Selects the final semantic match explanation from scored candidates.
/// </summary>
internal static class SemanticMatchSelector
{
    /// <summary>
    /// Selects a semantic match explanation from the supplied scoring result.
    /// </summary>
    /// <param name="scoringResult">The scored candidate data.</param>
    /// <param name="threshold">The configured similarity threshold.</param>
    /// <param name="requiredMargin">The configured ambiguity margin.</param>
    /// <returns>The semantic match explanation preserving the current selection semantics.</returns>
    public static SemanticMatchExplanation Select(
        SemanticCandidateScoringResult scoringResult,
        double threshold,
        double requiredMargin)
    {
        if (scoringResult.BestCandidate is null || scoringResult.BestScore is null)
        {
            return new SemanticMatchExplanation
            {
                Attempted = true,
                BestCandidate = scoringResult.HighestScoringCandidate,
                BestScore = scoringResult.HighestScore,
                Threshold = threshold,
                RequiredMargin = requiredMargin,
                SelectionStatus = scoringResult.HighestScore is null
                    ? SemanticSelectionStatus.Unavailable
                    : SemanticSelectionStatus.BelowThreshold,
                NonSelectionReason = scoringResult.HighestScore is null
                    ? SemanticSelectionStatus.Unavailable
                    : SemanticSelectionStatus.BelowThreshold,
                CandidateScores = scoringResult.CandidateScores,
            };
        }

        double? marginToSecondBest = scoringResult.SecondBestScore.HasValue
            ? scoringResult.BestScore.Value - scoringResult.SecondBestScore.Value
            : null;

        if (scoringResult.SecondBestScore is not null &&
            marginToSecondBest < requiredMargin)
        {
            return new SemanticMatchExplanation
            {
                Attempted = true,
                BestCandidate = scoringResult.HighestScoringCandidate,
                BestScore = scoringResult.HighestScore,
                SecondBestCandidate = scoringResult.SecondHighestScoringCandidate,
                Threshold = threshold,
                RequiredMargin = requiredMargin,
                SelectedScore = scoringResult.BestScore,
                SecondBestScore = scoringResult.SecondBestScore,
                MarginToSecondBest = marginToSecondBest,
                SelectionStatus = SemanticSelectionStatus.Ambiguous,
                NonSelectionReason = SemanticSelectionStatus.Ambiguous,
                CandidateScores = scoringResult.CandidateScores,
            };
        }

        return new SemanticMatchExplanation
        {
            Attempted = true,
            SelectedCandidate = scoringResult.BestCandidate,
            BestCandidate = scoringResult.HighestScoringCandidate,
            BestScore = scoringResult.HighestScore,
            SecondBestCandidate = scoringResult.SecondHighestScoringCandidate,
            SelectedScore = scoringResult.BestScore,
            Threshold = threshold,
            RequiredMargin = requiredMargin,
            SecondBestScore = scoringResult.SecondBestScore,
            MarginToSecondBest = marginToSecondBest,
            SelectionStatus = SemanticSelectionStatus.Selected,
            CandidateScores = scoringResult.CandidateScores,
        };
    }
}
