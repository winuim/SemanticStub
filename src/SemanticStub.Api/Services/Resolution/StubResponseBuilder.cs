using SemanticStub.Api.Models;
using SemanticStub.Api.Utilities;

namespace SemanticStub.Api.Services;

internal sealed class StubResponseBuilder
{
    private const string JsonContentType = "application/json";
    private readonly Func<string, string> responseFileReader;

    public StubResponseBuilder(Func<string, string> responseFileReader)
    {
        this.responseFileReader = responseFileReader;
    }

    public bool TryBuild(int statusCode, ResponseDefinition responseDefinition, out StubResponse response)
    {
        response = null!;

        if (!string.IsNullOrEmpty(responseDefinition.ResponseFile))
        {
            response = CreateStubResponse(
                statusCode,
                responseDefinition.DelayMilliseconds,
                responseDefinition.Content,
                responseDefinition.Headers,
                responseDefinition.ResponseFile);

            return true;
        }

        var responseBody = BuildResponseBody(responseDefinition.Content);

        if (responseBody is null)
        {
            return false;
        }

        response = CreateStubResponse(
            statusCode,
            responseDefinition.DelayMilliseconds,
            responseDefinition.Content,
            responseDefinition.Headers,
            responseBody,
            filePath: null);

        return true;
    }

    public bool TryBuild(QueryMatchResponseDefinition responseDefinition, out StubResponse response)
    {
        response = null!;

        if (responseDefinition.StatusCode <= 0)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(responseDefinition.ResponseFile))
        {
            response = CreateStubResponse(
                responseDefinition.StatusCode,
                responseDefinition.DelayMilliseconds,
                responseDefinition.Content,
                responseDefinition.Headers,
                responseDefinition.ResponseFile);

            return true;
        }

        var responseBody = BuildResponseBody(responseDefinition.Content);

        if (responseBody is null)
        {
            return false;
        }

        response = CreateStubResponse(
            responseDefinition.StatusCode,
            responseDefinition.DelayMilliseconds,
            responseDefinition.Content,
            responseDefinition.Headers,
            responseBody,
            filePath: null);

        return true;
    }

    private static StubResponse CreateStubResponse(
        int statusCode,
        int? delayMilliseconds,
        IReadOnlyDictionary<string, MediaTypeDefinition> content,
        IReadOnlyDictionary<string, HeaderDefinition> headers,
        string responseBody,
        string? filePath)
    {
        return new StubResponse
        {
            StatusCode = statusCode,
            DelayMilliseconds = delayMilliseconds,
            ContentType = ResolveContentType(content),
            Headers = StubResponseHeaderBuilder.BuildResponseHeaders(headers),
            Body = responseBody,
            FilePath = filePath
        };
    }

    private StubResponse CreateStubResponse(
        int statusCode,
        int? delayMilliseconds,
        IReadOnlyDictionary<string, MediaTypeDefinition> content,
        IReadOnlyDictionary<string, HeaderDefinition> headers,
        string responseFile)
    {
        if (Path.IsPathRooted(responseFile))
        {
            return CreateStubResponse(
                statusCode,
                delayMilliseconds,
                content,
                headers,
                string.Empty,
                responseFile);
        }

        return CreateStubResponse(
            statusCode,
            delayMilliseconds,
            content,
            headers,
            responseFileReader(responseFile),
            filePath: null);
    }

    private static string? BuildResponseBody(IReadOnlyDictionary<string, MediaTypeDefinition> content)
    {
        var selectedKey = SelectMediaTypeKey(content);

        if (selectedKey is null || !content.TryGetValue(selectedKey, out var mediaType) || mediaType.Example is null)
        {
            return null;
        }

        if (!IsJsonContentType(selectedKey) && mediaType.Example is string rawExample)
        {
            return rawExample;
        }

        return StubExampleSerializer.Serialize(mediaType.Example);
    }

    private static string ResolveContentType(IReadOnlyDictionary<string, MediaTypeDefinition> content)
    {
        return SelectMediaTypeKey(content) ?? JsonContentType;
    }

    private static string? SelectMediaTypeKey(IReadOnlyDictionary<string, MediaTypeDefinition> content)
    {
        return content.Keys.FirstOrDefault(IsJsonContentType) ?? content.Keys.FirstOrDefault();
    }

    private static bool IsJsonContentType(string contentType)
    {
        return contentType.Equals(JsonContentType, StringComparison.OrdinalIgnoreCase)
            || contentType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }
}
