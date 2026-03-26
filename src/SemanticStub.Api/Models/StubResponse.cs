namespace SemanticStub.Api.Models;

public sealed class StubResponse
{
    public int StatusCode { get; init; }

    public string ContentType { get; init; } = "application/json";

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string Body { get; init; } = string.Empty;
}
