namespace SemanticStub.Api.Models;

public sealed class ResponseDefinition
{
    public string Description { get; init; } = string.Empty;

    public Dictionary<string, MediaTypeDefinition> Content { get; init; } = new(StringComparer.Ordinal);
}
