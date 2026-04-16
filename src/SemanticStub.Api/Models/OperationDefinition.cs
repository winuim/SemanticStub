using YamlDotNet.Serialization;

namespace SemanticStub.Api.Models;

/// <summary>
/// Describes an OpenAPI operation and its stub-specific match definitions.
/// </summary>
public sealed class OperationDefinition
{
    /// <summary>
    /// Gets the OpenAPI operation identifier used to distinguish the operation.
    /// </summary>
    public string OperationId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the OpenAPI parameters accepted by the operation.
    /// </summary>
    public List<ParameterDefinition> Parameters { get; init; } = [];

    /// <summary>
    /// Gets the ordered stub match definitions declared by the <c>x-match</c> extension.
    /// </summary>
    [YamlMember(Alias = "x-match", ApplyNamingConventions = false)]
    public List<QueryMatchDefinition> Matches { get; init; } = [];

    /// <summary>
    /// Gets the OpenAPI responses keyed by status code.
    /// </summary>
    public Dictionary<string, ResponseDefinition> Responses { get; init; } = new(StringComparer.Ordinal);
}
