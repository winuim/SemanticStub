using System.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SemanticStub.Application.Infrastructure.Yaml;
using SemanticStub.Application.Models;

namespace SemanticStub.Application.Services.Semantic;

/// <summary>
/// Calls an external Text Embeddings Inference service to score optional semantic fallback candidates.
/// </summary>
public sealed class SemanticMatcherService : ISemanticMatcherService
{
    private readonly ISemanticEmbeddingClient _embeddingClient;
    private readonly StubSettings _settings;
    private readonly ILogger<SemanticMatcherService> _logger;
    private readonly ConcurrentDictionary<string, float[]> _candidateEmbeddingCache = new(StringComparer.Ordinal);

    public SemanticMatcherService(
        ISemanticEmbeddingClient embeddingClient,
        StubSettings settings,
        ILogger<SemanticMatcherService> logger)
    {
        _settings = settings;
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
        bool includeCandidateScores = false,
        CancellationToken cancellationToken = default)
    {
        var semanticSettings = _settings.SemanticMatching;
        var normalizedMethod = method.ToUpperInvariant();
        var totalStopwatch = Stopwatch.StartNew();

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

            var embeddingStopwatch = Stopwatch.StartNew();
            var allEmbeddings = await GetEmbeddingsAsync(
                method,
                path,
                query,
                headers,
                body,
                semanticCandidates,
                cancellationToken);
            embeddingStopwatch.Stop();

            _logger.LogDebug(
                "Semantic embedding request completed for '{Path}' {Method}. ElapsedMs={ElapsedMs}, Candidates={CandidateCount}.",
                path,
                normalizedMethod,
                Math.Max(0, embeddingStopwatch.Elapsed.TotalMilliseconds),
                semanticCandidates.Count);

            var explanation = ScoreAndLogExplanation(
                path,
                normalizedMethod,
                semanticCandidates,
                allEmbeddings,
                semanticSettings.Threshold,
                semanticSettings.TopScoreMargin,
                includeCandidateScores);

            totalStopwatch.Stop();

            _logger.LogInformation(
                "Semantic matching completed for '{Path}' {Method}. ElapsedMs={ElapsedMs}, MatchSelected={MatchSelected}, CandidateCount={CandidateCount}.",
                path,
                normalizedMethod,
                Math.Max(0, totalStopwatch.Elapsed.TotalMilliseconds),
                explanation.SelectedCandidate is not null,
                semanticCandidates.Count);

            return explanation;
        }
        catch (HttpRequestException ex)
        {
            totalStopwatch.Stop();
            _logger.LogWarning(
                ex,
                "Semantic matching failed for '{Path}' {Method}: the embedding endpoint request failed after {ElapsedMs}ms. Treating the request as a non-match.",
                path,
                normalizedMethod,
                Math.Max(0, totalStopwatch.Elapsed.TotalMilliseconds));
            return CreateFailedExplanation(semanticSettings);
        }
        catch (TaskCanceledException ex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalStopwatch.Stop();
            _logger.LogWarning(
                ex,
                "Semantic matching failed for '{Path}' {Method}: the embedding endpoint request timed out after {ElapsedMs}ms. Treating the request as a non-match.",
                path,
                normalizedMethod,
                Math.Max(0, totalStopwatch.Elapsed.TotalMilliseconds));
            return CreateFailedExplanation(semanticSettings);
        }
        catch (InvalidOperationException ex)
        {
            totalStopwatch.Stop();
            _logger.LogWarning(
                ex,
                "Semantic matching failed for '{Path}' {Method}: the embedding endpoint returned an unexpected response after {ElapsedMs}ms. Treating the request as a non-match.",
                path,
                normalizedMethod,
                Math.Max(0, totalStopwatch.Elapsed.TotalMilliseconds));
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
        IReadOnlyList<QueryMatchDefinition> semanticCandidates,
        CancellationToken cancellationToken)
    {
        var requestText = SemanticRequestTextBuilder.Build(method, path, query, headers, body);
        var candidateTexts = semanticCandidates
            .Select(candidate => candidate.SemanticMatch!)
            .ToArray();
        var missingCandidateTexts = candidateTexts
            .Where(text => !_candidateEmbeddingCache.ContainsKey(text))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var textsToEmbed = new List<string>(missingCandidateTexts.Length + 1)
        {
            requestText
        };
        textsToEmbed.AddRange(missingCandidateTexts);

        var newEmbeddings = await _embeddingClient.GetEmbeddingsAsync(textsToEmbed, cancellationToken);
        var requestEmbedding = newEmbeddings[0];

        for (var i = 0; i < missingCandidateTexts.Length; i++)
        {
            _candidateEmbeddingCache[missingCandidateTexts[i]] = newEmbeddings[i + 1];
        }

        var allEmbeddings = new List<float[]>(semanticCandidates.Count + 1)
        {
            requestEmbedding
        };
        allEmbeddings.AddRange(candidateTexts.Select(text => _candidateEmbeddingCache[text]));

        return allEmbeddings;
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
