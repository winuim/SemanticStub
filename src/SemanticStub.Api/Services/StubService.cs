using System.Collections;
using System.Text.Json;
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
        return TryGetResponse(method, path, new Dictionary<string, string>(StringComparer.Ordinal), body: null, out response);
    }

    public StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, string> query, out StubResponse response)
    {
        return TryGetResponse(method, path, query, body: null, out response);
    }

    public StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, string> query, string? body, out StubResponse response)
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

        var queryMatchResult = TryBuildMatchedQueryResponse(operation, query, body, out response);

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

    private QueryMatchEvaluationResult TryBuildMatchedQueryResponse(
        OperationDefinition operation,
        IReadOnlyDictionary<string, string> query,
        string? body,
        out StubResponse response)
    {
        response = null!;

        if (operation.Matches.Count == 0)
        {
            return QueryMatchEvaluationResult.NoMatch;
        }

        using var bodyDocument = ParseRequestBody(body);
        var matchedCandidate = operation.Matches
            .Where(candidate => IsExactQueryMatch(candidate.Query, query))
            .Where(candidate => IsBodyMatch(candidate.Body, bodyDocument?.RootElement))
            .OrderByDescending(GetMatchSpecificity)
            .FirstOrDefault();

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

    private static bool IsExactQueryMatch(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var value) || value != pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static JsonDocument? ParseRequestBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsBodyMatch(object? expectedBody, JsonElement? actualBody)
    {
        if (expectedBody is null)
        {
            return true;
        }

        if (actualBody is null)
        {
            return false;
        }

        var expectedJson = StubDefinitionLoader.SerializeExample(expectedBody);
        using var expectedDocument = JsonDocument.Parse(expectedJson);

        return IsJsonMatch(expectedDocument.RootElement, actualBody.Value);
    }

    private static bool IsJsonMatch(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind == JsonValueKind.Object)
        {
            if (actual.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in expected.EnumerateObject())
            {
                if (!actual.TryGetProperty(property.Name, out var actualProperty) ||
                    !IsJsonMatch(property.Value, actualProperty))
                {
                    return false;
                }
            }

            return true;
        }

        if (expected.ValueKind == JsonValueKind.Array)
        {
            if (actual.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var expectedItems = expected.EnumerateArray().ToArray();
            var actualItems = actual.EnumerateArray().ToArray();

            if (expectedItems.Length != actualItems.Length)
            {
                return false;
            }

            for (var index = 0; index < expectedItems.Length; index++)
            {
                if (!IsJsonMatch(expectedItems[index], actualItems[index]))
                {
                    return false;
                }
            }

            return true;
        }

        if (expected.ValueKind != actual.ValueKind)
        {
            return false;
        }

        return expected.ValueKind switch
        {
            JsonValueKind.String => expected.GetString() == actual.GetString(),
            JsonValueKind.Number => expected.GetRawText() == actual.GetRawText(),
            JsonValueKind.True or JsonValueKind.False => expected.GetBoolean() == actual.GetBoolean(),
            JsonValueKind.Null => true,
            _ => expected.GetRawText() == actual.GetRawText()
        };
    }

    private static int GetMatchSpecificity(QueryMatchDefinition match)
    {
        return match.Query.Count + GetBodySpecificity(match.Body);
    }

    private static int GetBodySpecificity(object? body)
    {
        return body switch
        {
            null => 0,
            IDictionary dictionary => dictionary.Count + dictionary.Values.Cast<object?>().Sum(GetBodySpecificity),
            IEnumerable list when body is not string => list.Cast<object?>().Sum(GetBodySpecificity),
            _ => 1
        };
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

        return StubDefinitionLoader.SerializeExample(mediaType.Example);
    }
}
