namespace SemanticStub.Api.Services;

internal static class SemanticEmbeddingEndpoint
{
    internal static string Normalize(string endpoint)
    {
        var normalized = endpoint.TrimEnd('/');
        return normalized.EndsWith("/embed", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized + "/embed";
    }
}
