namespace SemanticStub.Api.Models;

public sealed class HeaderDefinition
{
    public string Description { get; init; } = string.Empty;

    public object? Example { get; init; }

    public HeaderSchemaDefinition? Schema { get; init; }
}
