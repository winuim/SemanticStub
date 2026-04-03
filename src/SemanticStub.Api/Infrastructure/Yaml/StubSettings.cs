namespace SemanticStub.Api.Infrastructure.Yaml;

public sealed class StubSettings
{
    public string? DefinitionsPath { get; init; }

    public SemanticMatchingSettings SemanticMatching { get; init; } = new();
}
