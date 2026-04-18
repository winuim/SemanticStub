namespace SemanticStub.Api.Models;

public sealed class ParameterDefinition
{
    public string Name { get; init; } = string.Empty;

    public string In { get; init; } = string.Empty;

    public ParameterSchemaDefinition? Schema { get; init; }
}
