namespace SemanticStub.Api.Models;

public sealed class StubResponse
{
    public int StatusCode { get; init; }

    public string ContentType { get; init; } = "application/json";

    public string Body { get; init; } = string.Empty;
}
