namespace SemanticStub.Application.Models;

public sealed class PathItemDefinition
{
    public List<ParameterDefinition> Parameters { get; init; } = [];

    public OperationDefinition? Get { get; init; }

    public OperationDefinition? Post { get; init; }

    public OperationDefinition? Put { get; init; }

    public OperationDefinition? Patch { get; init; }

    public OperationDefinition? Delete { get; init; }
}
