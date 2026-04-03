using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
    private readonly HttpClient httpClient;
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
        this.httpClient = httpClient;
        this.settings = settings.Value;
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
        var semanticSettings = settings.SemanticMatching;
        var normalizedMethod = method.ToUpperInvariant();

        if (!IsEnabled(out var disabledReason))
        {
            logger.LogDebug(
                "Semantic matching skipped for '{Path}' {Method}: {Reason}",
                path,
                normalizedMethod,
                disabledReason);
            return null;
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
            return null;
        }

        try
        {
            var requestText = BuildRequestText(method, path, query, headers, body);
            var endpoint = NormalizeEndpoint(semanticSettings.Endpoint!);

            logger.LogInformation(
                "Semantic matching started for '{Path}' {Method}. Endpoint={Endpoint}, Threshold={Threshold}, TopScoreMargin={TopScoreMargin}, Candidates={CandidateCount}.",
                path,
                normalizedMethod,
                endpoint,
                semanticSettings.Threshold,
                semanticSettings.TopScoreMargin,
                semanticCandidates.Length);
            var requestEmbedding = await GetEmbeddingAsync(requestText).ConfigureAwait(false);
            QueryMatchDefinition? bestCandidate = null;
            double? bestScore = null;
            double? secondBestScore = null;

            foreach (var candidate in semanticCandidates)
            {
                var candidateText = candidate.SemanticMatch!;
                var candidateEmbedding = await GetEmbeddingAsync(candidateText).ConfigureAwait(false);
                var score = CosineSimilarity(requestEmbedding, candidateEmbedding);

                logger.LogDebug(
                    "Semantic candidate scored for '{Path}' {Method}. Candidate='{SemanticMatch}', Score={Score}, Threshold={Threshold}.",
                    path,
                    normalizedMethod,
                    candidateText,
                    score,
                    semanticSettings.Threshold);

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
                return null;
            }

            if (secondBestScore is not null &&
                bestScore.Value - secondBestScore.Value < semanticSettings.TopScoreMargin)
            {
                logger.LogDebug(
                    "Semantic matching did not select a candidate for '{Path}' {Method}. Top score margin {Margin} was below the required {RequiredMargin}.",
                    path,
                    normalizedMethod,
                    bestScore.Value - secondBestScore.Value,
                    semanticSettings.TopScoreMargin);
                return null;
            }

            logger.LogInformation(
                "Semantic matching selected candidate for '{Path}' {Method}. Candidate='{SemanticMatch}', Score={Score}.",
                path,
                normalizedMethod,
                bestCandidate.SemanticMatch,
                bestScore.Value);

            return bestCandidate;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Semantic matching failed for '{Path}' {Method}. Treating the request as a non-match.",
                path,
                normalizedMethod);
            return null;
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

    private async Task<float[]> GetEmbeddingAsync(string input)
    {
        var endpoint = NormalizeEndpoint(settings.SemanticMatching.Endpoint!);
        var response = await httpClient.PostAsJsonAsync(endpoint, new EmbedRequest(input)).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(responseStream).ConfigureAwait(false);

        if (TryReadEmbedding(document.RootElement, out var embedding))
        {
            return embedding;
        }

        throw new InvalidOperationException("The embedding endpoint returned an unexpected response shape.");
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        var normalized = endpoint.TrimEnd('/');
        return normalized.EndsWith("/embed", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized + "/embed";
    }

    private static bool TryReadEmbedding(JsonElement element, out float[] embedding)
    {
        embedding = [];

        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        if (element.GetArrayLength() == 0)
        {
            return false;
        }

        var firstElement = element[0];

        if (firstElement.ValueKind == JsonValueKind.Number)
        {
            embedding = element
                .EnumerateArray()
                .Select(value => value.GetSingle())
                .ToArray();
            return embedding.Length > 0;
        }

        if (firstElement.ValueKind == JsonValueKind.Array)
        {
            embedding = firstElement
                .EnumerateArray()
                .Select(value => value.GetSingle())
                .ToArray();
            return embedding.Length > 0;
        }

        return false;
    }

    private static string BuildRequestText(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        var builder = new StringBuilder();
        builder.Append("method: ").Append(method.ToUpperInvariant()).AppendLine();
        builder.Append("path: ").Append(path).AppendLine();

        if (query.Count > 0)
        {
            builder.AppendLine("query:");

            foreach (var pair in query.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                builder.Append("  ")
                    .Append(pair.Key)
                    .Append(": ")
                    .AppendLine(string.Join(", ", pair.Value.Select(value => value ?? string.Empty)));
            }
        }

        if (headers.Count > 0)
        {
            builder.AppendLine("headers:");

            foreach (var pair in headers.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append("  ")
                    .Append(pair.Key)
                    .Append(": ")
                    .AppendLine(pair.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            builder.AppendLine("body:");
            builder.Append(body.Trim());
        }

        return builder.ToString();
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

    private sealed record EmbedRequest(string Inputs);
}
