using Microsoft.Extensions.Primitives;

namespace SemanticStub.Api.Models;

public sealed class StubResponse
{
    public int StatusCode { get; init; }

    public int? DelayMilliseconds { get; init; }

    public string ContentType { get; init; } = "application/json";

    public IReadOnlyDictionary<string, StringValues> Headers { get; init; } = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

    public string Body { get; init; } = string.Empty;

    public string? FilePath { get; init; }
}
