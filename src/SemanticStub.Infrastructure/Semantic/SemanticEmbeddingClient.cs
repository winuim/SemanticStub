using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SemanticStub.Application.Infrastructure.Yaml;
using SemanticStub.Application.Services.Semantic;

namespace SemanticStub.Infrastructure.Semantic;

/// <summary>
/// Calls the configured embedding endpoint and validates the returned embedding payloads.
/// </summary>
internal sealed class SemanticEmbeddingClient : ISemanticEmbeddingClient
{
    private const string HttpClientName = "SemanticEmbedding";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemanticMatchingSettings _settings;

    /// <summary>
    /// Creates a client over the configured embedding endpoint settings.
    /// </summary>
    /// <param name="httpClientFactory">The factory used to create the configured embedding endpoint client.</param>
    /// <param name="settings">The stub runtime settings that provide the semantic endpoint configuration.</param>
    public SemanticEmbeddingClient(IHttpClientFactory httpClientFactory, IOptions<StubSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value.SemanticMatching;
    }

    /// <summary>
    /// Gets embeddings for the supplied inputs from the configured embedding endpoint.
    /// </summary>
    /// <param name="inputs">The request and candidate texts to embed.</param>
    /// <returns>The returned embedding vectors in request order.</returns>
    /// <exception cref="HttpRequestException">Thrown when the embedding endpoint request fails.</exception>
    /// <exception cref="TaskCanceledException">Thrown when the embedding endpoint request times out.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response shape is invalid.</exception>
    public async Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var endpoint = SemanticEmbeddingEndpoint.Normalize(_settings.Endpoint!);
        var response = await httpClient.PostAsJsonAsync(endpoint, new EmbedRequest(inputs), cancellationToken);
        response.EnsureSuccessStatusCode();

        try
        {
            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

            if (TryReadEmbeddings(document.RootElement, inputs.Count, out var embeddings))
            {
                return embeddings;
            }

            throw new InvalidOperationException("The embedding endpoint returned an unexpected response shape.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("The embedding endpoint returned an invalid JSON response.", ex);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("The embedding endpoint returned an invalid numeric value.", ex);
        }
    }

    private static bool TryReadEmbeddings(JsonElement root, int expectedCount, out IReadOnlyList<float[]> embeddings)
    {
        embeddings = [];

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != expectedCount)
        {
            return false;
        }

        var result = new float[expectedCount][];

        for (var i = 0; i < expectedCount; i++)
        {
            var element = root[i];

            if (element.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            result[i] = element.EnumerateArray()
                .Select(v => v.GetSingle())
                .ToArray();

            if (result[i].Length == 0)
            {
                return false;
            }
        }

        embeddings = result;
        return true;
    }

    private sealed record EmbedRequest(IReadOnlyList<string> Inputs);
}
