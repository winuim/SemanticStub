using YamlDotNet.Serialization;

namespace SemanticStub.Api.Models;

public sealed class QueryMatchDefinition
{
    public Dictionary<string, object?> Query { get; init; } = new(StringComparer.Ordinal);

    [YamlMember(Alias = "x-query-partial", ApplyNamingConventions = false)]
    public Dictionary<string, object?> PartialQuery { get; init; } = new(StringComparer.Ordinal);

    [YamlMember(Alias = "x-query-regex", ApplyNamingConventions = false)]
    public Dictionary<string, object?> RegexQuery { get; init; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public object? Body { get; init; }

    public QueryMatchResponseDefinition Response { get; init; } = new();
}
