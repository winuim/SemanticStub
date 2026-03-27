using YamlDotNet.Serialization;

namespace SemanticStub.Api.Models;

public sealed class QueryMatchResponseDefinition
{
    public int StatusCode { get; init; }

    [YamlMember(Alias = "x-delay", ApplyNamingConventions = false)]
    public int? DelayMilliseconds { get; init; }

    [YamlMember(Alias = "x-response-file", ApplyNamingConventions = false)]
    public string? ResponseFile { get; init; }

    public Dictionary<string, HeaderDefinition> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, MediaTypeDefinition> Content { get; init; } = new(StringComparer.Ordinal);
}
