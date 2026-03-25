using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

public sealed class StubService
{
    private const string JsonContentType = "application/json";
    private readonly StubDocument document;

    public StubService(StubDefinitionLoader loader)
    {
        document = loader.LoadDefaultDefinition();
    }

    public StubService(StubDocument document)
    {
        this.document = document;
    }

    public StubMatchResult TryGetResponse(string method, string path, out StubResponse response)
    {
        response = null!;

        if (!document.Paths.TryGetValue(path, out var pathItem))
        {
            return StubMatchResult.PathNotFound;
        }

        var operation = GetOperation(method, pathItem);

        if (operation is null)
        {
            return StubMatchResult.MethodNotAllowed;
        }

        var matchedResponse = operation.Responses
            .FirstOrDefault(entry =>
                int.TryParse(entry.Key, out _) &&
                entry.Value.Content.ContainsKey(JsonContentType));

        if (string.IsNullOrEmpty(matchedResponse.Key) ||
            !int.TryParse(matchedResponse.Key, out var statusCode))
        {
            return StubMatchResult.ResponseNotConfigured;
        }

        if (!matchedResponse.Value.Content.TryGetValue(JsonContentType, out var mediaType))
        {
            return StubMatchResult.ResponseNotConfigured;
        }

        response = new StubResponse
        {
            StatusCode = statusCode,
            ContentType = JsonContentType,
            Body = StubDefinitionLoader.SerializeExample(mediaType.Example)
        };

        return StubMatchResult.Matched;
    }

    private static OperationDefinition? GetOperation(string method, PathItemDefinition pathItem)
    {
        if (HttpMethods.IsGet(method))
        {
            return pathItem.Get;
        }

        if (HttpMethods.IsPost(method))
        {
            return pathItem.Post;
        }

        return null;
    }
}
