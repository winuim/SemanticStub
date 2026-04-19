namespace SemanticStub.Application.Services.Semantic;

public static class SemanticEmbeddingEndpoint
{
    public static string Normalize(string endpoint)
    {
        var normalized = endpoint.TrimEnd('/');
        return normalized.EndsWith("/embed", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized + "/embed";
    }
}
