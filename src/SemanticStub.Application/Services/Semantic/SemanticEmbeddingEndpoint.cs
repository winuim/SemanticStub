namespace SemanticStub.Application.Services.Semantic;

/// <summary>
/// Normalizes embedding endpoint URLs for use with the Text Embeddings Inference API.
/// </summary>
public static class SemanticEmbeddingEndpoint
{
    /// <summary>
    /// Ensures the endpoint URL ends with <c>/embed</c>, appending it if absent.
    /// </summary>
    /// <param name="endpoint">The raw endpoint URL to normalize.</param>
    /// <returns>The normalized endpoint URL with a trailing <c>/embed</c> path segment.</returns>
    public static string Normalize(string endpoint)
    {
        var normalized = endpoint.TrimEnd('/');
        return normalized.EndsWith("/embed", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized + "/embed";
    }
}
