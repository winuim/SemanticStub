using YamlDotNet.Serialization;

namespace SemanticStub.Api.Models;

/// <summary>
/// Represents the OpenAPI document used as the YAML source of truth for stub definitions.
/// </summary>
public sealed class StubDocument
{
    /// <summary>
    /// Gets the OpenAPI version declared by the document.
    /// </summary>
    [YamlMember(Alias = "openapi")]
    public string OpenApi { get; init; } = string.Empty;

    /// <summary>
    /// Gets the OpenAPI path definitions keyed by route template.
    /// </summary>
    public Dictionary<string, PathItemDefinition> Paths { get; init; } = new(StringComparer.Ordinal);
}
