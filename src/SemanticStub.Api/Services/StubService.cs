using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;
using SemanticStub.Api.Utilities;

namespace SemanticStub.Api.Services;

public sealed class StubService
{
    private const string JsonContentType = "application/json";
    private readonly StubDocument document;
    private readonly Func<string, string> responseFileReader;
    private readonly MatcherService matcherService;

    public StubService(StubDefinitionLoader loader)
        : this(loader, new MatcherService())
    {
    }

    public StubService(StubDefinitionLoader loader, MatcherService matcherService)
    {
        document = loader.LoadDefaultDefinition();
        responseFileReader = loader.LoadResponseFileContent;
        this.matcherService = matcherService;
    }

    public StubService(StubDocument document)
        : this(document, _ => throw new InvalidOperationException("No response file reader configured."), new MatcherService())
    {
    }

    public StubService(StubDocument document, Func<string, string> responseFileReader)
        : this(document, responseFileReader, new MatcherService())
    {
    }

    internal StubService(StubDocument document, Func<string, string> responseFileReader, MatcherService matcherService)
    {
        this.document = document;
        this.responseFileReader = responseFileReader;
        this.matcherService = matcherService;
    }

    public StubMatchResult TryGetResponse(string method, string path, out StubResponse response)
    {
        return TryGetResponse(
            method,
            path,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null,
            out response);
    }

    public StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, string> query, out StubResponse response)
    {
        return TryGetResponse(
            method,
            path,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null,
            out response);
    }

    public StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, string> query, string? body, out StubResponse response)
    {
        return TryGetResponse(
            method,
            path,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body,
            out response);
    }

    public StubMatchResult TryGetResponse(
        string method,
        string path,
        IReadOnlyDictionary<string, string> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        out StubResponse response)
    {
        response = null!;

        var pathItem = ResolvePathItem(path);

        if (pathItem is null)
        {
            return StubMatchResult.PathNotFound;
        }

        var operation = GetOperation(method, pathItem);

        if (operation is null)
        {
            return StubMatchResult.MethodNotAllowed;
        }

        var queryMatchResult = TryBuildMatchedQueryResponse(operation, query, headers, body, out response);

        if (queryMatchResult == QueryMatchEvaluationResult.Matched)
        {
            return StubMatchResult.Matched;
        }

        if (queryMatchResult == QueryMatchEvaluationResult.MatchedButInvalidResponse)
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

        var responseBody = BuildResponseBody(matchedResponse.Value);

        if (responseBody is null)
        {
            return StubMatchResult.ResponseNotConfigured;
        }

        response = new StubResponse
        {
            StatusCode = statusCode,
            ContentType = JsonContentType,
            Body = responseBody
        };

        return StubMatchResult.Matched;
    }

    private PathItemDefinition? ResolvePathItem(string requestPath)
    {
        if (document.Paths.TryGetValue(requestPath, out var exactPathItem))
        {
            return exactPathItem;
        }

        return document.Paths
            .Where(entry => IsTemplateMatch(entry.Key, requestPath))
            .OrderByDescending(entry => GetTemplateSpecificity(entry.Key))
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => entry.Value)
            .FirstOrDefault();
    }

    private static bool IsTemplateMatch(string templatePath, string requestPath)
    {
        var templateSegments = GetPathSegments(templatePath);
        var requestSegments = GetPathSegments(requestPath);

        if (templateSegments.Length != requestSegments.Length)
        {
            return false;
        }

        for (var index = 0; index < templateSegments.Length; index++)
        {
            var templateSegment = templateSegments[index];
            var requestSegment = requestSegments[index];

            if (IsPathParameterSegment(templateSegment))
            {
                if (string.IsNullOrEmpty(requestSegment))
                {
                    return false;
                }

                continue;
            }

            if (!string.Equals(templateSegment, requestSegment, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static int GetTemplateSpecificity(string templatePath)
    {
        return GetPathSegments(templatePath).Count(segment => !IsPathParameterSegment(segment));
    }

    private static string[] GetPathSegments(string path)
    {
        return path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool IsPathParameterSegment(string segment)
    {
        return segment.Length > 2 &&
               segment[0] == '{' &&
               segment[^1] == '}' &&
               !segment[1..^1].Contains('{') &&
               !segment[1..^1].Contains('}');
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

        if (HttpMethods.IsPut(method))
        {
            return pathItem.Put;
        }

        if (HttpMethods.IsPatch(method))
        {
            return pathItem.Patch;
        }

        if (HttpMethods.IsDelete(method))
        {
            return pathItem.Delete;
        }

        return null;
    }

    private QueryMatchEvaluationResult TryBuildMatchedQueryResponse(
        OperationDefinition operation,
        IReadOnlyDictionary<string, string> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        out StubResponse response)
    {
        response = null!;

        if (operation.Matches.Count == 0)
        {
            return QueryMatchEvaluationResult.NoMatch;
        }

        var matchedCandidate = matcherService.FindBestMatch(operation, query, headers, body);

        if (matchedCandidate is null)
        {
            return QueryMatchEvaluationResult.NoMatch;
        }

        var responseBody = BuildResponseBody(matchedCandidate.Response.ResponseFile, matchedCandidate.Response.Content);

        if (responseBody is null || matchedCandidate.Response.StatusCode <= 0)
        {
            return QueryMatchEvaluationResult.MatchedButInvalidResponse;
        }

        response = new StubResponse
        {
            StatusCode = matchedCandidate.Response.StatusCode,
            ContentType = JsonContentType,
            Body = responseBody
        };

        return QueryMatchEvaluationResult.Matched;
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

        if (mediaType.Example is null)
        {
            return null;
        }

        return StubExampleSerializer.Serialize(mediaType.Example);
    }
}
