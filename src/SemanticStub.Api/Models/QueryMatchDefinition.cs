using YamlDotNet.Serialization;

namespace SemanticStub.Api.Models;

public sealed class QueryMatchDefinition
{
    public Dictionary<string, string> Query { get; init; } = new(StringComparer.Ordinal);

    public QueryMatchResponseDefinition Response { get; init; } = new();
}
