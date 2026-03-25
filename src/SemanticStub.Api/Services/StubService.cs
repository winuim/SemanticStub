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
        return TryGetResponse(method, path, new Dictionary<string, string>(StringComparer.Ordinal), out response);
    }

    public StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, string> query, out StubResponse response)
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

        if (TryBuildMatchedQueryResponse(operation, query, out response))
        {
            return StubMatchResult.Matched;
        }

        if (operation.Matches.Count > 0)
        {
            return StubMatchResult.ResponseNotConfigured;
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

    private bool TryBuildMatchedQueryResponse(OperationDefinition operation, IReadOnlyDictionary<string, string> query, out StubResponse response)
    {
        response = null!;

        if (operation.Matches.Count == 0)
        {
            return false;
        }

        var matchedCandidate = operation.Matches
            .Where(candidate => IsExactQueryMatch(candidate.Query, query))
            .OrderByDescending(candidate => candidate.Query.Count)
            .FirstOrDefault();

        if (matchedCandidate is null)
        {
            return false;
        }

        var body = BuildResponseBody(matchedCandidate.Response.ResponseFile, matchedCandidate.Response.Content);

        if (body is null || matchedCandidate.Response.StatusCode <= 0)
        {
            return false;
        }

        response = new StubResponse
        {
            StatusCode = matchedCandidate.Response.StatusCode,
            ContentType = JsonContentType,
            Body = body
        };

        return true;
    }

    private static bool IsExactQueryMatch(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var value) || value != pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    private string? BuildResponseBody(ResponseDefinition responseDefinition)
    {
        return BuildResponseBody(responseDefinition.ResponseFile, responseDefinition.Content);
    }

    private string? BuildResponseBody(string? responseFile, IReadOnlyDictionary<string, MediaTypeDefinition> content)
    {
        if (!string.IsNullOrEmpty(responseFile))
        {
            return responseFileReader(responseFile);
        }

        if (!content.TryGetValue(JsonContentType, out var mediaType))
        {
            return null;
        }

        return StubDefinitionLoader.SerializeExample(mediaType.Example);
    }
}
