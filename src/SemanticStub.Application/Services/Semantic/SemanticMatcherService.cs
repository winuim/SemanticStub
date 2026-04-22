using System.Diagnostics;
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
    private readonly IStubDefinitionVersionProvider _definitionVersionProvider;
    private readonly StubSettings _settings;
    private readonly ILogger<SemanticMatcherService> _logger;
    private readonly object _cacheSyncRoot = new();
    private CandidateEmbeddingCacheState _candidateEmbeddingCache;

    public SemanticMatcherService(
        ISemanticEmbeddingClient embeddingClient,
        IStubDefinitionVersionProvider definitionVersionProvider,
        StubSettings settings,
        ILogger<SemanticMatcherService> logger)
    {
        _definitionVersionProvider = definitionVersionProvider;
        _settings = settings;
        _embeddingClient = embeddingClient;
        _logger = logger;
        _candidateEmbeddingCache = CandidateEmbeddingCacheState.CreateEmpty(definitionVersionProvider.CurrentVersion);
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
        var cacheSnapshot = InvalidateCacheIfDefinitionsReloaded();

        var requestText = SemanticRequestTextBuilder.Build(method, path, query, headers, body);
        var candidateTexts = semanticCandidates
            .Select(candidate => candidate.SemanticMatch!)
            .ToArray();
        var missingCandidateTexts = candidateTexts
            .Where(text => !cacheSnapshot.Embeddings.ContainsKey(text))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var textsToEmbed = new List<string>(missingCandidateTexts.Length + 1)
        {
            requestText
        };
        textsToEmbed.AddRange(missingCandidateTexts);

        var embeddings = await _embeddingClient.GetEmbeddingsAsync(textsToEmbed, cancellationToken);
        var requestEmbedding = embeddings[0];
        IReadOnlyList<float[]> candidateEmbeddings = embeddings.Skip(1).ToArray();

        if (cacheSnapshot.EmbeddingDimension.HasValue &&
            cacheSnapshot.EmbeddingDimension.Value != requestEmbedding.Length)
        {
            cacheSnapshot = ReplaceCache(CandidateEmbeddingCacheState.CreateEmpty(cacheSnapshot.DefinitionVersion));
            missingCandidateTexts = candidateTexts
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            candidateEmbeddings = missingCandidateTexts.Length == 0
                ? []
                : await _embeddingClient.GetEmbeddingsAsync(missingCandidateTexts, cancellationToken);
        }

        var candidateEmbeddingsByText = new Dictionary<string, float[]>(StringComparer.Ordinal);
        var newlyFetchedEmbeddings = new Dictionary<string, float[]>(StringComparer.Ordinal);

        for (var i = 0; i < missingCandidateTexts.Length; i++)
        {
            newlyFetchedEmbeddings[missingCandidateTexts[i]] = candidateEmbeddings[i];
        }

        if (newlyFetchedEmbeddings.Count > 0)
        {
            cacheSnapshot = PublishCandidateEmbeddings(
                cacheSnapshot.DefinitionVersion,
                candidateEmbeddings[0].Length,
                newlyFetchedEmbeddings);
        }

        foreach (var text in candidateTexts.Distinct(StringComparer.Ordinal))
        {
            if (newlyFetchedEmbeddings.TryGetValue(text, out var embedding) ||
                cacheSnapshot.Embeddings.TryGetValue(text, out embedding))
            {
                candidateEmbeddingsByText[text] = embedding;
            }
        }

        var allEmbeddings = new List<float[]>(semanticCandidates.Count + 1)
        {
            requestEmbedding
        };
        allEmbeddings.AddRange(candidateTexts.Select(text => candidateEmbeddingsByText[text]));

        return allEmbeddings;
    }

    private CandidateEmbeddingCacheState InvalidateCacheIfDefinitionsReloaded()
    {
        var currentDefinitionVersion = _definitionVersionProvider.CurrentVersion;
        var cacheSnapshot = Volatile.Read(ref _candidateEmbeddingCache);

        if (currentDefinitionVersion == cacheSnapshot.DefinitionVersion)
        {
            return cacheSnapshot;
        }

        return ReplaceCache(CandidateEmbeddingCacheState.CreateEmpty(currentDefinitionVersion));
    }

    private CandidateEmbeddingCacheState ReplaceCache(CandidateEmbeddingCacheState nextState)
    {
        lock (_cacheSyncRoot)
        {
            Volatile.Write(ref _candidateEmbeddingCache, nextState);
            return nextState;
        }
    }

    private CandidateEmbeddingCacheState PublishCandidateEmbeddings(
        long definitionVersion,
        int embeddingDimension,
        IReadOnlyDictionary<string, float[]> embeddings)
    {
        lock (_cacheSyncRoot)
        {
            var currentCache = Volatile.Read(ref _candidateEmbeddingCache);

            if (currentCache.DefinitionVersion != definitionVersion)
            {
                return currentCache;
            }

            var mergedEmbeddings = new Dictionary<string, float[]>(currentCache.Embeddings, StringComparer.Ordinal);

            foreach (var pair in embeddings)
            {
                mergedEmbeddings[pair.Key] = pair.Value;
            }

            var nextState = new CandidateEmbeddingCacheState(
                definitionVersion,
                embeddingDimension,
                mergedEmbeddings);
            Volatile.Write(ref _candidateEmbeddingCache, nextState);
            return nextState;
        }
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

    private sealed record CandidateEmbeddingCacheState(
        long DefinitionVersion,
        int? EmbeddingDimension,
        IReadOnlyDictionary<string, float[]> Embeddings)
    {
        public static CandidateEmbeddingCacheState CreateEmpty(long definitionVersion)
        {
            return new CandidateEmbeddingCacheState(
                definitionVersion,
                EmbeddingDimension: null,
                new Dictionary<string, float[]>(StringComparer.Ordinal));
        }
    }
}
