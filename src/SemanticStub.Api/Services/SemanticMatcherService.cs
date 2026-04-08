using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

/// <summary>
/// Calls an external Text Embeddings Inference service to score optional semantic fallback candidates.
/// </summary>
public sealed class SemanticMatcherService : ISemanticMatcherService
{
    private readonly SemanticEmbeddingClient embeddingClient;
    private readonly StubSettings settings;
    private readonly ILogger<SemanticMatcherService> logger;

    /// <summary>
    /// Creates a semantic matcher over the configured Text Embeddings Inference endpoint.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call the configured embedding endpoint.</param>
    /// <param name="settings">The stub runtime settings, including semantic matching configuration.</param>
    /// <param name="logger">The logger used to record semantic matching scores and failures.</param>
    public SemanticMatcherService(
        HttpClient httpClient,
        IOptions<StubSettings> settings,
        ILogger<SemanticMatcherService> logger)
    {
        this.settings = settings.Value;
        embeddingClient = new SemanticEmbeddingClient(httpClient, this.settings.SemanticMatching);
        this.logger = logger;
    }

    /// <summary>
    /// Finds the best semantic match among the supplied conditional candidates.
    /// </summary>
    public async Task<QueryMatchDefinition?> FindBestMatchAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        IReadOnlyCollection<QueryMatchDefinition> candidates,
        Func<QueryMatchDefinition, bool>? candidateFilter = null)
    {
        var explanation = await ExplainMatchAsync(
            method,
            path,
            query,
            headers,
            body,
            candidates,
            candidateFilter,
            includeCandidateScores: false).ConfigureAwait(false);

        return explanation.SelectedCandidate;
    }

    /// <inheritdoc/>
    public async Task<SemanticMatchExplanation> ExplainMatchAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        IReadOnlyCollection<QueryMatchDefinition> candidates,
        Func<QueryMatchDefinition, bool>? candidateFilter = null,
        bool includeCandidateScores = false)
    {
        var semanticSettings = settings.SemanticMatching;
        var normalizedMethod = method.ToUpperInvariant();

        if (!IsEnabled(out var disabledReason))
        {
            logger.LogDebug(
                "Semantic matching skipped for '{Path}' {Method}: {Reason}",
                path,
                normalizedMethod,
                disabledReason);
            return new SemanticMatchExplanation();
        }

        var semanticCandidates = candidates
            .Where(candidate => candidateFilter is null || candidateFilter(candidate))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.SemanticMatch))
            .ToArray();

        if (semanticCandidates.Length == 0)
        {
            logger.LogDebug(
                "Semantic matching skipped for '{Path}' {Method}: no eligible candidates define x-semantic-match.",
                path,
                normalizedMethod);
            return new SemanticMatchExplanation();
        }

        try
        {
            var requestText = SemanticRequestTextBuilder.Build(method, path, query, headers, body);
            var endpoint = SemanticEmbeddingClient.NormalizeEndpoint(semanticSettings.Endpoint!);

            logger.LogInformation(
                "Semantic matching started for '{Path}' {Method}. Endpoint={Endpoint}, Threshold={Threshold}, TopScoreMargin={TopScoreMargin}, Candidates={CandidateCount}.",
                path,
                normalizedMethod,
                endpoint,
                semanticSettings.Threshold,
                semanticSettings.TopScoreMargin,
                semanticCandidates.Length);

            var allTexts = new List<string>(semanticCandidates.Length + 1) { requestText };
            allTexts.AddRange(semanticCandidates.Select(c => c.SemanticMatch!));

            var allEmbeddings = await embeddingClient.GetEmbeddingsAsync(allTexts).ConfigureAwait(false);
            var requestEmbedding = allEmbeddings[0];
            var candidateScores = includeCandidateScores
                ? new List<SemanticCandidateScore>(semanticCandidates.Length)
                : null;

            QueryMatchDefinition? bestCandidate = null;
            double? bestScore = null;
            double? secondBestScore = null;

            for (var i = 0; i < semanticCandidates.Length; i++)
            {
                var candidate = semanticCandidates[i];
                var candidateText = candidate.SemanticMatch!;
                var candidateEmbedding = allEmbeddings[i + 1];
                var score = CosineSimilarity(requestEmbedding, candidateEmbedding);

                logger.LogDebug(
                    "Semantic candidate scored for '{Path}' {Method}. Candidate='{SemanticMatch}', Score={Score}, Threshold={Threshold}.",
                    path,
                    normalizedMethod,
                    candidateText,
                    score,
                    semanticSettings.Threshold);

                candidateScores?.Add(new SemanticCandidateScore
                {
                    Candidate = candidate,
                    Eligible = true,
                    Score = score,
                    AboveThreshold = score >= semanticSettings.Threshold,
                });

                if (score < semanticSettings.Threshold)
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

            if (bestCandidate is null || bestScore is null)
            {
                logger.LogDebug(
                    "Semantic matching did not select a candidate for '{Path}' {Method}. Best score stayed below the threshold.",
                    path,
                    normalizedMethod);
                return new SemanticMatchExplanation
                {
                    Attempted = true,
                    Threshold = semanticSettings.Threshold,
                    RequiredMargin = semanticSettings.TopScoreMargin,
                    CandidateScores = candidateScores ?? [],
                };
            }

            double? marginToSecondBest = secondBestScore.HasValue
                ? bestScore.Value - secondBestScore.Value
                : null;

            if (secondBestScore is not null &&
                marginToSecondBest < semanticSettings.TopScoreMargin)
            {
                logger.LogDebug(
                    "Semantic matching did not select a candidate for '{Path}' {Method}. Top score margin {Margin} was below the required {RequiredMargin}.",
                    path,
                    normalizedMethod,
                    marginToSecondBest,
                    semanticSettings.TopScoreMargin);
                return new SemanticMatchExplanation
                {
                    Attempted = true,
                    Threshold = semanticSettings.Threshold,
                    RequiredMargin = semanticSettings.TopScoreMargin,
                    SelectedScore = bestScore,
                    SecondBestScore = secondBestScore,
                    MarginToSecondBest = marginToSecondBest,
                    CandidateScores = candidateScores ?? [],
                };
            }

            logger.LogInformation(
                "Semantic matching selected candidate for '{Path}' {Method}. Candidate='{SemanticMatch}', Score={Score}.",
                path,
                normalizedMethod,
                bestCandidate.SemanticMatch,
                bestScore.Value);

            return new SemanticMatchExplanation
            {
                Attempted = true,
                SelectedCandidate = bestCandidate,
                SelectedScore = bestScore,
                Threshold = semanticSettings.Threshold,
                RequiredMargin = semanticSettings.TopScoreMargin,
                SecondBestScore = secondBestScore,
                MarginToSecondBest = marginToSecondBest,
                CandidateScores = candidateScores ?? [],
            };
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "Semantic matching failed for '{Path}' {Method}: the embedding endpoint request failed. Treating the request as a non-match.",
                path,
                normalizedMethod);
            return new SemanticMatchExplanation
            {
                Attempted = true,
                Threshold = semanticSettings.Threshold,
                RequiredMargin = semanticSettings.TopScoreMargin,
            };
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(
                ex,
                "Semantic matching failed for '{Path}' {Method}: the embedding endpoint request timed out. Treating the request as a non-match.",
                path,
                normalizedMethod);
            return new SemanticMatchExplanation
            {
                Attempted = true,
                Threshold = semanticSettings.Threshold,
                RequiredMargin = semanticSettings.TopScoreMargin,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Semantic matching encountered an unexpected error for '{Path}' {Method}. Treating the request as a non-match.",
                path,
                normalizedMethod);
            return new SemanticMatchExplanation
            {
                Attempted = true,
                Threshold = semanticSettings.Threshold,
                RequiredMargin = semanticSettings.TopScoreMargin,
            };
        }
    }

    private bool IsEnabled(out string reason)
    {
        var semanticSettings = settings.SemanticMatching;

        if (!semanticSettings.Enabled)
        {
            reason = "SemanticMatching.Enabled is false.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(semanticSettings.Endpoint))
        {
            reason = "SemanticMatching.Endpoint is empty.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
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
