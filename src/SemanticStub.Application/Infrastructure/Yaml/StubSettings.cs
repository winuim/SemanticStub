namespace SemanticStub.Application.Infrastructure.Yaml;

/// <summary>
/// Configures the YAML stub definition source and optional matching features.
/// </summary>
public sealed class StubSettings
{
    /// <summary>
    /// Gets the path to the YAML stub definition file or directory.
    /// </summary>
    public string? DefinitionsPath { get; init; }

    /// <summary>
    /// Gets the semantic matching configuration.
    /// </summary>
    public SemanticMatchingSettings SemanticMatching { get; init; } = new();
}
