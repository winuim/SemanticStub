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
            var scoringResult = SemanticCandidateScorer.ScoreCandidates(
                semanticCandidates,
                allEmbeddings[0],
                allEmbeddings.Skip(1).ToArray(),
                semanticSettings.Threshold,
                includeCandidateScores,
                (candidate, score) =>
                {
                    logger.LogDebug(
                        "Semantic candidate scored for '{Path}' {Method}. Candidate='{SemanticMatch}', Score={Score}, Threshold={Threshold}.",
                        path,
                        normalizedMethod,
                        candidate.SemanticMatch,
                        score,
                        semanticSettings.Threshold);
                });

            var explanation = SemanticMatchSelector.Select(
                scoringResult,
                semanticSettings.Threshold,
                semanticSettings.TopScoreMargin);

            if (explanation.SelectedCandidate is null && explanation.SelectedScore is null)
            {
                logger.LogDebug(
                    "Semantic matching did not select a candidate for '{Path}' {Method}. Best score stayed below the threshold.",
                    path,
                    normalizedMethod);
                return explanation;
            }

            if (explanation.SelectedCandidate is null && explanation.SelectedScore is not null)
            {
                logger.LogDebug(
                    "Semantic matching did not select a candidate for '{Path}' {Method}. Top score margin {Margin} was below the required {RequiredMargin}.",
                    path,
                    normalizedMethod,
                    explanation.MarginToSecondBest,
                    semanticSettings.TopScoreMargin);
                return explanation;
            }

            logger.LogInformation(
                "Semantic matching selected candidate for '{Path}' {Method}. Candidate='{SemanticMatch}', Score={Score}.",
                path,
                normalizedMethod,
                explanation.SelectedCandidate!.SemanticMatch,
                explanation.SelectedScore!.Value);

            return explanation;
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
}
