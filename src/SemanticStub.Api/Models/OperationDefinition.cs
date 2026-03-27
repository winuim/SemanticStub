using YamlDotNet.Serialization;

namespace SemanticStub.Api.Models;

public sealed class OperationDefinition
{
    public string OperationId { get; init; } = string.Empty;

    public List<ParameterDefinition> Parameters { get; init; } = [];

    [YamlMember(Alias = "x-match", ApplyNamingConventions = false)]
    public List<QueryMatchDefinition> Matches { get; init; } = [];

    public Dictionary<string, ResponseDefinition> Responses { get; init; } = new(StringComparer.Ordinal);
}
