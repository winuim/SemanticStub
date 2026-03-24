namespace SemanticStub.Api.Models;

public sealed class OperationDefinition
{
    public string OperationId { get; init; } = string.Empty;

    public Dictionary<string, ResponseDefinition> Responses { get; init; } = new(StringComparer.Ordinal);
}
