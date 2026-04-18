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
    private readonly ISemanticEmbeddingClient _embeddingClient;
    private readonly StubSettings _settings;
    private readonly ILogger<SemanticMatcherService> _logger;

    internal SemanticMatcherService(
        ISemanticEmbeddingClient embeddingClient,
        IOptions<StubSettings> settings,
        ILogger<SemanticMatcherService> logger)
    {
        _settings = settings.Value;
        _embeddingClient = embeddingClient;
        _logger = logger;
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
        var semanticSettings = _settings.SemanticMatching;
        var normalizedMethod = method.ToUpperInvariant();

        if (!IsEnabled(out var disabledReason))
        {
            _logger.LogDebug(
                "Semantic matching skipped for '{Path}' {Method}: {Reason}",
                path,
                normalizedMethod,
                disabledReason);
            return new SemanticMatchExplanation();
        }

        var semanticCandidates = GetSemanticCandidates(candidates, candidateFilter);

        if (semanticCandidates.Count == 0)
        {
            _logger.LogDebug(
                "Semantic matching skipped for '{Path}' {Method}: no eligible candidates define x-semantic-match.",
                path,
                normalizedMethod);
            return new SemanticMatchExplanation();
        }

        try
        {
            var endpoint = SemanticEmbeddingEndpoint.Normalize(semanticSettings.Endpoint!);

            _logger.LogInformation(
                "Semantic matching started for '{Path}' {Method}. Endpoint={Endpoint}, Threshold={Threshold}, TopScoreMargin={TopScoreMargin}, Candidates={CandidateCount}.",
                path,
                normalizedMethod,
                endpoint,
                semanticSettings.Threshold,
                semanticSettings.TopScoreMargin,
                semanticCandidates.Count);

            var allEmbeddings = await GetEmbeddingsAsync(
                method,
                path,
                query,
                headers,
                body,
                semanticCandidates);

            return ScoreAndLogExplanation(
                path,
                normalizedMethod,
                semanticCandidates,
                allEmbeddings,
                semanticSettings.Threshold,
                semanticSettings.TopScoreMargin,
                includeCandidateScores);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Semantic matching failed for '{Path}' {Method}: the embedding endpoint request failed. Treating the request as a non-match.",
                path,
                normalizedMethod);
            return CreateFailedExplanation(semanticSettings);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Semantic matching failed for '{Path}' {Method}: the embedding endpoint request timed out. Treating the request as a non-match.",
                path,
                normalizedMethod);
            return CreateFailedExplanation(semanticSettings);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Semantic matching encountered an unexpected error for '{Path}' {Method}. Treating the request as a non-match.",
                path,
                normalizedMethod);
            return CreateFailedExplanation(semanticSettings);
        }
    }

    private bool IsEnabled(out string reason)
    {
        var semanticSettings = _settings.SemanticMatching;

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

    private static IReadOnlyList<QueryMatchDefinition> GetSemanticCandidates(
        IReadOnlyCollection<QueryMatchDefinition> candidates,
        Func<QueryMatchDefinition, bool>? candidateFilter)
    {
        return candidates
            .Where(candidate => candidateFilter is null || candidateFilter(candidate))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.SemanticMatch))
            .ToArray();
    }

    private async Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        IReadOnlyList<QueryMatchDefinition> semanticCandidates)
    {
        var allTexts = new List<string>(semanticCandidates.Count + 1)
        {
            SemanticRequestTextBuilder.Build(method, path, query, headers, body)
        };

        allTexts.AddRange(semanticCandidates.Select(candidate => candidate.SemanticMatch!));
        return await _embeddingClient.GetEmbeddingsAsync(allTexts);
    }

    private SemanticMatchExplanation ScoreAndLogExplanation(
        string path,
        string normalizedMethod,
        IReadOnlyList<QueryMatchDefinition> semanticCandidates,
        IReadOnlyList<float[]> allEmbeddings,
        double threshold,
        double requiredMargin,
        bool includeCandidateScores)
    {
        var scoringResult = SemanticCandidateScorer.ScoreCandidates(
            semanticCandidates,
            allEmbeddings[0],
            allEmbeddings.Skip(1).ToArray(),
            threshold,
            includeCandidateScores,
            (candidate, score) =>
            {
                _logger.LogDebug(
                    "Semantic candidate scored for '{Path}' {Method}. Candidate='{SemanticMatch}', Score={Score}, Threshold={Threshold}.",
                    path,
                    normalizedMethod,
                    candidate.SemanticMatch,
                    score,
                    threshold);
            });

        var explanation = SemanticMatchSelector.Select(scoringResult, threshold, requiredMargin);

        if (explanation.SelectedCandidate is null && explanation.SelectedScore is null)
        {
            _logger.LogDebug(
                "Semantic matching did not select a candidate for '{Path}' {Method}. Best score stayed below the threshold.",
                path,
                normalizedMethod);
            return explanation;
        }

        if (explanation.SelectedCandidate is null && explanation.SelectedScore is not null)
        {
            _logger.LogDebug(
                "Semantic matching did not select a candidate for '{Path}' {Method}. Top score margin {Margin} was below the required {RequiredMargin}.",
                path,
                normalizedMethod,
                explanation.MarginToSecondBest,
                requiredMargin);
            return explanation;
        }

        _logger.LogInformation(
            "Semantic matching selected candidate for '{Path}' {Method}. Candidate='{SemanticMatch}', Score={Score}.",
            path,
            normalizedMethod,
            explanation.SelectedCandidate!.SemanticMatch,
            explanation.SelectedScore!.Value);

        return explanation;
    }

    private static SemanticMatchExplanation CreateFailedExplanation(SemanticMatchingSettings semanticSettings)
    {
        return new SemanticMatchExplanation
        {
            Attempted = true,
            Threshold = semanticSettings.Threshold,
            RequiredMargin = semanticSettings.TopScoreMargin,
        };
    }

}
