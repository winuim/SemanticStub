using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemanticStub.Api.Inspection;

/// <summary>
/// Generates reviewable draft YAML stub definitions from recorded requests.
/// The output follows OpenAPI 3.1 conventions with x-* extensions used by SemanticStub.
/// </summary>
public static class DraftYamlExporter
{
    private static readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .DisableAliases()
        .Build();

    // Headers excluded from x-match because they are sensitive or unreliable for stub matching.
    private static readonly HashSet<string> _excludedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Proxy-Authorization",
        "Accept",
        "Accept-Encoding",
        "Accept-Language",
        "User-Agent",
        "Referer",
        "Origin",
        "Content-Type",
    };

    /// <summary>
    /// Exports a recorded request as a draft YAML stub definition.
    /// </summary>
    /// <param name="request">The recorded request to use as a basis for the draft.</param>
    /// <returns>A YAML string containing a minimal but valid OpenAPI 3.1 stub entry.</returns>
    public static string Export(ReplayReadyRequestInfo request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Export([request]);
    }

    /// <summary>
    /// Exports recorded requests as grouped draft YAML stub suggestions.
    /// </summary>
    /// <param name="requests">The recorded requests to use as a basis for the suggestions.</param>
    /// <returns>A YAML string containing reviewable OpenAPI 3.1 stub suggestions.</returns>
    public static string Export(IEnumerable<ReplayReadyRequestInfo> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var requestList = requests.ToList();
        if (requestList.Any(request => request is null))
        {
            throw new ArgumentException("Requests must not contain null entries.", nameof(requests));
        }

        var paths = new Dictionary<string, object>(StringComparer.Ordinal);
        var hasSkippedBodyFields = false;

        foreach (var pathGroup in requestList.GroupBy(r => r.Path, StringComparer.Ordinal))
        {
            var pathItem = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (var operationGroup in pathGroup.GroupBy(r => r.Method, StringComparer.OrdinalIgnoreCase))
            {
                var operationRequests = operationGroup.ToList();
                var operation = BuildOperation(operationRequests, out var operationHasSkippedBodyFields);
                pathItem[operationGroup.Key.ToLowerInvariant()] = operation;
                hasSkippedBodyFields |= operationHasSkippedBodyFields;
            }

            paths[pathGroup.Key] = pathItem;
        }

        var document = new Dictionary<string, object>
        {
            ["openapi"] = "3.1.0",
            ["info"] = new Dictionary<string, object>
            {
                ["title"] = "Draft Stub",
                ["version"] = "0.0.0",
            },
            ["paths"] = paths,
        };

        var yaml = _serializer.Serialize(document);

        if (hasSkippedBodyFields)
        {
            yaml += "# TODO: body contained nested fields that were skipped — complete x-match.body manually\n";
        }

        return yaml;
    }

    private static Dictionary<string, object> BuildOperation(
        IReadOnlyList<ReplayReadyRequestInfo> requests,
        out bool hasSkippedBodyFields)
    {
        var firstRequest = requests[0];

        var operation = new Dictionary<string, object>
        {
            ["operationId"] = BuildOperationId(firstRequest.Method, firstRequest.Path),
        };

        var matchEntries = new List<object>();
        hasSkippedBodyFields = false;

        foreach (var request in requests)
        {
            var matchEntry = BuildMatchEntry(request, out var requestHasSkippedBodyFields);
            hasSkippedBodyFields |= requestHasSkippedBodyFields;

            if (matchEntry is not null)
            {
                matchEntries.Add(matchEntry);
            }
        }

        if (matchEntries.Count > 0)
        {
            operation["x-match"] = matchEntries;
        }

        var requestBodyContent = BuildRequestBodyContent(requests);
        if (requestBodyContent.Count > 0)
        {
            operation["requestBody"] = new Dictionary<string, object>
            {
                ["content"] = requestBodyContent,
            };
        }

        operation["responses"] = new Dictionary<string, object>
        {
            ["200"] = new Dictionary<string, object>
            {
                ["description"] = "TODO",
                ["content"] = new Dictionary<string, object>
                {
                    ["application/json"] = new Dictionary<string, object>
                    {
                        ["example"] = new Dictionary<string, object>(),
                    },
                },
            },
        };

        return operation;
    }

    private static Dictionary<string, object>? BuildMatchEntry(
        ReplayReadyRequestInfo request,
        out bool hasSkippedBodyFields)
    {
        var matchHeaders = BuildMatchHeaders(request.Headers);
        var (matchBody, skippedBodyFields) = BuildMatchBody(request.Body, request.Headers);
        hasSkippedBodyFields = skippedBodyFields;

        if (request.Query is not { Count: > 0 } && matchHeaders.Count == 0
            && matchBody is null && !hasSkippedBodyFields)
        {
            return null;
        }

        var matchEntry = new Dictionary<string, object>();

        if (request.Query is { Count: > 0 })
        {
            var queryMap = new Dictionary<string, object>();
            foreach (var (key, values) in request.Query.OrderBy(q => q.Key, StringComparer.Ordinal))
            {
                queryMap[key] = values.Length == 1 ? (object)values[0] : values;
            }
            matchEntry["query"] = queryMap;
        }

        if (matchHeaders.Count > 0)
        {
            var headerMap = new Dictionary<string, object>();
            foreach (var (key, value) in matchHeaders.OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase))
            {
                headerMap[key] = value;
            }
            matchEntry["headers"] = headerMap;
        }

        if (matchBody is not null)
        {
            var bodyMap = new Dictionary<string, object?>();
            foreach (var (key, value) in matchBody.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                bodyMap[key] = value;
            }
            matchEntry["body"] = bodyMap;
        }

        matchEntry["response"] = new Dictionary<string, object>
        {
            ["statusCode"] = 200,
            ["content"] = new Dictionary<string, object>
            {
                ["application/json"] = new Dictionary<string, object>
                {
                    ["example"] = new Dictionary<string, object>(),
                },
            },
        };

        return matchEntry;
    }

    private static Dictionary<string, object> BuildRequestBodyContent(IEnumerable<ReplayReadyRequestInfo> requests)
    {
        var content = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var request in requests)
        {
            var contentType = DetectContentType(request.Headers);
            if (string.IsNullOrEmpty(request.Body) || contentType is null || content.ContainsKey(contentType))
            {
                continue;
            }

            content[contentType] = new Dictionary<string, object>
            {
                ["example"] = new Dictionary<string, object>(),
            };
        }

        return content;
    }

    private static string BuildOperationId(string method, string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var suffix = string.Concat(segments.Select(SafeSegment));
        return method.ToLowerInvariant() + suffix;
    }

    private static string SafeSegment(string segment)
    {
        // Strip path-parameter braces: {id} → Id, {orderId} → OrderId
        if (segment.StartsWith('{') && segment.EndsWith('}'))
        {
            segment = segment[1..^1];
        }

        // Remove non-alphanumeric characters and capitalise the first letter.
        var clean = new string(segment.Where(char.IsLetterOrDigit).ToArray());
        if (clean.Length == 0)
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(clean[0]) + clean[1..];
    }

    private static Dictionary<string, string> BuildMatchHeaders(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null || headers.Count == 0)
        {
            return [];
        }

        return headers
            .Where(h => !_excludedHeaders.Contains(h.Key))
            .ToDictionary(h => h.Key, h => h.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static (Dictionary<string, object?>? body, bool hasSkippedBodyFields) BuildMatchBody(
        string? body, IReadOnlyDictionary<string, string>? headers)
    {
        if (string.IsNullOrEmpty(body))
        {
            return (null, false);
        }

        var contentType = DetectContentType(headers);
        if (!IsJsonContentType(contentType))
        {
            return (null, false);
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, false);
            }

            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            var skipped = false;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    result[property.Name] = property.Value.GetString() ?? string.Empty;
                }
                else
                {
                    // Non-string scalars (numbers, booleans, null) and nested structures are
                    // skipped: StubDefinitionLoader deserializes all YAML scalars as strings,
                    // so a typed condition like `count: 42` would be loaded as the string "42"
                    // and fail to match the JSON number 42 in JsonBodyMatcher.
                    skipped = true;
                }
            }

            // Preserve the skipped signal even when no mappable fields were extracted.
            return result.Count > 0 ? (result, skipped) : (null, skipped);
        }
        catch (JsonException)
        {
            return (null, false);
        }
    }

    private static string? DetectContentType(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return null;
        }

        return headers.TryGetValue("Content-Type", out var ct) ? ct.Split(';')[0].Trim() : null;
    }

    // P1: treat any +json media type (e.g. application/vnd.api+json) as JSON.
    private static bool IsJsonContentType(string? contentType) =>
        contentType is not null &&
        (contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
         contentType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
}
