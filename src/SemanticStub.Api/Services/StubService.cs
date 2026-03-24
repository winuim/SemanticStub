using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

public sealed class StubService
{
    private const string JsonContentType = "application/json";
    private readonly StubDocument document;

    public StubService(StubDefinitionLoader loader)
    {
        document = loader.LoadHelloWorldDefinition();
    }

    public StubService(StubDocument document)
    {
        this.document = document;
    }

    public bool TryGetResponse(string method, string path, out StubResponse response)
    {
        response = null!;

        if (!HttpMethods.IsGet(method))
        {
            return false;
        }

        if (!document.Paths.TryGetValue(path, out var pathItem) || pathItem.Get is null)
        {
            return false;
        }

        var matchedResponse = pathItem.Get.Responses
            .FirstOrDefault(entry =>
                int.TryParse(entry.Key, out _) &&
                entry.Value.Content.ContainsKey(JsonContentType));

        if (string.IsNullOrEmpty(matchedResponse.Key) ||
            !int.TryParse(matchedResponse.Key, out var statusCode))
        {
            return false;
        }

        if (!matchedResponse.Value.Content.TryGetValue(JsonContentType, out var mediaType))
        {
            return false;
        }

        response = new StubResponse
        {
            StatusCode = statusCode,
            ContentType = JsonContentType,
            Body = StubDefinitionLoader.SerializeExample(mediaType.Example)
        };

        return true;
    }
}
