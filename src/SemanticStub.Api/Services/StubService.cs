using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;
using SemanticStub.Api.Utilities;
using Microsoft.Extensions.Primitives;
using System.Collections;
using System.Globalization;

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
                (entry.Value.Content.Count > 0 || !string.IsNullOrEmpty(entry.Value.ResponseFile)));

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
            DelayMilliseconds = matchedResponse.Value.DelayMilliseconds,
            ContentType = ResolveContentType(matchedResponse.Value.Content),
            Headers = BuildResponseHeaders(matchedResponse.Value.Headers),
            Body = responseBody
        };

        return StubMatchResult.Matched;
    }

    private PathItemDefinition? ResolvePathItem(string requestPath)
    {
        // Keep deterministic routing: exact paths always win before template paths.
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

        // Query/header/body conditions are combined, then the most specific surviving candidate wins.
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
            DelayMilliseconds = matchedCandidate.Response.DelayMilliseconds,
            ContentType = ResolveContentType(matchedCandidate.Response.Content),
            Headers = BuildResponseHeaders(matchedCandidate.Response.Headers),
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

        var selectedKey = SelectMediaTypeKey(content);

        if (selectedKey is null || !content.TryGetValue(selectedKey, out var mediaType) || mediaType.Example is null)
        {
            return null;
        }

        // Non-JSON string examples are returned as-is; JSON types go through serialization.
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

    // Prefer JSON content types to preserve deterministic behavior for stubs that declare
    // multiple media types (e.g. application/json alongside text/plain for documentation).
    // Fall back to the first declared entry only when no JSON type is present.
    private static string? SelectMediaTypeKey(IReadOnlyDictionary<string, MediaTypeDefinition> content)
    {
        return content.Keys.FirstOrDefault(IsJsonContentType) ?? content.Keys.FirstOrDefault();
    }

    private static bool IsJsonContentType(string contentType)
    {
        return contentType.Equals(JsonContentType, StringComparison.OrdinalIgnoreCase)
            || contentType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, StringValues> BuildResponseHeaders(IReadOnlyDictionary<string, HeaderDefinition> headers)
    {
        if (headers.Count == 0)
        {
            return new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        }

        var resolvedHeaders = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            var resolvedValue = ResolveHeaderValue(header.Value);

            if (resolvedValue.Count == 0)
            {
                continue;
            }

            resolvedHeaders[header.Key] = string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase)
                ? resolvedValue
                : new StringValues(string.Join(", ", resolvedValue.ToArray().Where(static value => value is not null)!));
        }

        return resolvedHeaders;
    }

    private static StringValues ResolveHeaderValue(HeaderDefinition header)
    {
        return ConvertHeaderValueToStringValues(header.Example).Count > 0
            ? ConvertHeaderValueToStringValues(header.Example)
            : ConvertHeaderValueToStringValues(header.Schema?.Example);
    }

    private static StringValues ConvertHeaderValueToStringValues(object? value)
    {
        return value switch
        {
            null => StringValues.Empty,
            string text => new StringValues(text),
            char character => new StringValues(character.ToString()),
            bool boolean => new StringValues(boolean ? "true" : "false"),
            DateTime dateTime => new StringValues(dateTime.ToString("O", CultureInfo.InvariantCulture)),
            DateTimeOffset dateTimeOffset => new StringValues(dateTimeOffset.ToString("O", CultureInfo.InvariantCulture)),
            DateOnly dateOnly => new StringValues(dateOnly.ToString("O", CultureInfo.InvariantCulture)),
            TimeOnly timeOnly => new StringValues(timeOnly.ToString("O", CultureInfo.InvariantCulture)),
            Guid guid => new StringValues(guid.ToString()),
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
                => new StringValues(Convert.ToString(value, CultureInfo.InvariantCulture)),
            IFormattable formattable => new StringValues(formattable.ToString(format: null, CultureInfo.InvariantCulture)),
            IEnumerable sequence => ConvertHeaderSequenceToStringValues(sequence),
            _ => new StringValues(value.ToString())
        };
    }

    private static StringValues ConvertHeaderSequenceToStringValues(IEnumerable sequence)
    {
        var values = sequence
            .Cast<object?>()
            .SelectMany(static value => ConvertHeaderValueToStringValues(value).ToArray())
            .Where(static value => !string.IsNullOrEmpty(value))
            .ToArray();

        return values.Length == 0 ? StringValues.Empty : new StringValues(values);
    }
}
