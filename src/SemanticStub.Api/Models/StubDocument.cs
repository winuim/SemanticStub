using YamlDotNet.Serialization;

namespace SemanticStub.Api.Models;

public sealed class StubDocument
{
    [YamlMember(Alias = "openapi")]
    public string OpenApi { get; init; } = string.Empty;

    public Dictionary<string, PathItemDefinition> Paths { get; init; } = new(StringComparer.Ordinal);
}
