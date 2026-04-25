using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemanticStub.Api.Inspection;

/// <summary>
/// Generates a reviewable draft YAML stub definition from a single recorded request.
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

        var matchHeaders = BuildMatchHeaders(request.Headers);
        var (matchBody, hasSkippedNestedFields) = BuildMatchBody(request.Body, request.Headers);
        var hasXMatch = request.Query is { Count: > 0 } || matchHeaders.Count > 0
            || matchBody is not null || hasSkippedNestedFields;

        var operation = new Dictionary<string, object>
        {
            ["operationId"] = BuildOperationId(request.Method, request.Path),
        };

        if (hasXMatch)
        {
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
                var bodyMap = new Dictionary<string, object>();
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

            operation["x-match"] = new List<object> { matchEntry };
        }

        var contentType = DetectContentType(request.Headers);
        if (!string.IsNullOrEmpty(request.Body) && contentType is not null)
        {
            operation["requestBody"] = new Dictionary<string, object>
            {
                ["content"] = new Dictionary<string, object>
                {
                    [contentType] = new Dictionary<string, object>
                    {
                        ["example"] = new Dictionary<string, object>(),
                    },
                },
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

        var document = new Dictionary<string, object>
        {
            ["openapi"] = "3.1.0",
            ["info"] = new Dictionary<string, object>
            {
                ["title"] = "Draft Stub",
                ["version"] = "0.0.0",
            },
            ["paths"] = new Dictionary<string, object>
            {
                [request.Path] = new Dictionary<string, object>
                {
                    [request.Method.ToLowerInvariant()] = operation,
                },
            },
        };

        var yaml = _serializer.Serialize(document);

        if (hasSkippedNestedFields)
        {
            yaml += "# TODO: body contained nested fields that were skipped — complete x-match.body manually\n";
        }

        return yaml;
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

    private static (Dictionary<string, string>? body, bool hasSkippedNestedFields) BuildMatchBody(
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

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var skipped = false;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null,
                };

                if (value is not null)
                {
                    result[property.Name] = value;
                }
                else
                {
                    skipped = true;
                }
            }

            // Preserve the skipped signal even when no scalar fields were extracted.
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
