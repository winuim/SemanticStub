using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

public sealed class StubService
{
    private const string JsonContentType = "application/json";
    private readonly StubDocument document;
    private readonly Func<string, string> responseFileReader;

    public StubService(StubDefinitionLoader loader)
    {
        document = loader.LoadDefaultDefinition();
        responseFileReader = loader.LoadResponseFileContent;
    }

    public StubService(StubDocument document)
    {
        this.document = document;
        responseFileReader = _ => throw new InvalidOperationException("No response file reader configured.");
    }

    public StubService(StubDocument document, Func<string, string> responseFileReader)
    {
        this.document = document;
        this.responseFileReader = responseFileReader;
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

        var body = BuildResponseBody(matchedResponse.Value);

        if (body is null)
        {
            return StubMatchResult.ResponseNotConfigured;
        }

        response = new StubResponse
        {
            StatusCode = statusCode,
            ContentType = JsonContentType,
            Body = body
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

    private string? BuildResponseBody(ResponseDefinition responseDefinition)
    {
        if (!string.IsNullOrEmpty(responseDefinition.ResponseFile))
        {
            return responseFileReader(responseDefinition.ResponseFile);
        }

        if (!responseDefinition.Content.TryGetValue(JsonContentType, out var mediaType))
        {
            return null;
        }

        return StubDefinitionLoader.SerializeExample(mediaType.Example);
    }
}
