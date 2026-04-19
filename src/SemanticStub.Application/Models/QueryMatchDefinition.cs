using YamlDotNet.Serialization;

namespace SemanticStub.Application.Models;

/// <summary>
/// Describes the request conditions and response selected by a stub match entry.
/// </summary>
public sealed class QueryMatchDefinition
{
    /// <summary>
    /// Gets the query parameters that must match exactly.
    /// </summary>
    public Dictionary<string, object?> Query { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the query parameters that must be present with partially matching values.
    /// </summary>
    [YamlMember(Alias = "x-query-partial", ApplyNamingConventions = false)]
    public Dictionary<string, object?> PartialQuery { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the query parameters whose values are matched by regular expressions.
    /// </summary>
    [YamlMember(Alias = "x-query-regex", ApplyNamingConventions = false)]
    public Dictionary<string, object?> RegexQuery { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the optional semantic match text declared by the <c>x-semantic-match</c> extension.
    /// </summary>
    [YamlMember(Alias = "x-semantic-match", ApplyNamingConventions = false)]
    public string? SemanticMatch { get; init; }

    /// <summary>
    /// Gets the request headers that must match, using case-insensitive header names.
    /// </summary>
    public Dictionary<string, object?> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the request body that must match.
    /// </summary>
    public object? Body { get; init; }

    /// <summary>
    /// Gets the response definition returned when this match entry is selected.
    /// </summary>
    public QueryMatchResponseDefinition Response { get; init; } = new();
}
