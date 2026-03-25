using YamlDotNet.Serialization;

namespace SemanticStub.Api.Models;

public sealed class ResponseDefinition
{
    public string Description { get; init; } = string.Empty;

    [YamlMember(Alias = "x-response-file", ApplyNamingConventions = false)]
    public string? ResponseFile { get; init; }

    public Dictionary<string, MediaTypeDefinition> Content { get; init; } = new(StringComparer.Ordinal);
}
