using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

/// <summary>
/// Scores semantic candidates against a request embedding.
/// </summary>
internal static class SemanticCandidateScorer
{
    /// <summary>
    /// Scores the supplied semantic candidates in request order.
    /// </summary>
    /// <param name="candidates">The semantic candidates to score.</param>
    /// <param name="requestEmbedding">The embedding of the incoming request text.</param>
    /// <param name="candidateEmbeddings">The embeddings of the candidate semantic texts.</param>
    /// <param name="threshold">The configured minimum acceptable score.</param>
    /// <param name="includeCandidateScores">Whether to capture per-candidate scores.</param>
    /// <param name="onCandidateScored">Optional callback invoked for each candidate score.</param>
    /// <returns>The scoring result used by candidate selection.</returns>
    public static SemanticCandidateScoringResult ScoreCandidates(
        IReadOnlyList<QueryMatchDefinition> candidates,
        IReadOnlyList<float> requestEmbedding,
        IReadOnlyList<float[]> candidateEmbeddings,
        double threshold,
        bool includeCandidateScores,
        Action<QueryMatchDefinition, double>? onCandidateScored = null)
    {
        var candidateScores = includeCandidateScores
            ? new List<SemanticCandidateScore>(candidates.Count)
            : null;

        QueryMatchDefinition? bestCandidate = null;
        double? bestScore = null;
        double? secondBestScore = null;

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var score = CosineSimilarity(requestEmbedding, candidateEmbeddings[i]);

            onCandidateScored?.Invoke(candidate, score);

            candidateScores?.Add(new SemanticCandidateScore
            {
                Candidate = candidate,
                Eligible = true,
                Score = score,
                AboveThreshold = score >= threshold,
            });

            if (score < threshold)
            {
                continue;
            }

            if (bestScore is null || score > bestScore.Value)
            {
                secondBestScore = bestScore;
                bestScore = score;
                bestCandidate = candidate;
            }
            else if (secondBestScore is null || score > secondBestScore.Value)
            {
                secondBestScore = score;
            }
        }

        return new SemanticCandidateScoringResult(
            bestCandidate,
            bestScore,
            secondBestScore,
            candidateScores ?? []);
    }

    internal static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left.Count == 0 || right.Count == 0 || left.Count != right.Count)
        {
            throw new InvalidOperationException("Embedding vectors must be non-empty and have the same dimension.");
        }

        double dot = 0d;
        double leftMagnitude = 0d;
        double rightMagnitude = 0d;

        for (var index = 0; index < left.Count; index++)
        {
            var leftValue = left[index];
            var rightValue = right[index];

            dot += leftValue * rightValue;
            leftMagnitude += leftValue * leftValue;
            rightMagnitude += rightValue * rightValue;
        }

        if (leftMagnitude == 0d || rightMagnitude == 0d)
        {
            throw new InvalidOperationException("Embedding vectors must not have zero magnitude.");
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }
}

/// <summary>
/// Holds the intermediate candidate scoring outcome used by semantic selection.
/// </summary>
internal sealed record SemanticCandidateScoringResult(
    QueryMatchDefinition? BestCandidate,
    double? BestScore,
    double? SecondBestScore,
    IReadOnlyList<SemanticCandidateScore> CandidateScores);
