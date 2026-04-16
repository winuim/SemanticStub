namespace SemanticStub.Api.Models;

/// <summary>
/// Describes an OpenAPI response header exposed by a stub response.
/// </summary>
public sealed class HeaderDefinition
{
    /// <summary>
    /// Gets the human-readable description of the response header.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the example value associated with the response header.
    /// </summary>
    public object? Example { get; init; }

    /// <summary>
    /// Gets the schema metadata associated with the response header.
    /// </summary>
    public HeaderSchemaDefinition? Schema { get; init; }
}
