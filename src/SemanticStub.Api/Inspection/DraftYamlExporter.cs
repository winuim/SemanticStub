using System.Text;
using System.Text.Json;

namespace SemanticStub.Api.Inspection;

/// <summary>
/// Generates a reviewable draft YAML stub definition from a single recorded request.
/// The output follows OpenAPI 3.1 conventions with x-* extensions used by SemanticStub.
/// </summary>
public static class DraftYamlExporter
{
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

        var method = request.Method.ToLowerInvariant();
        var operationId = BuildOperationId(request.Method, request.Path);
        var matchHeaders = BuildMatchHeaders(request.Headers);
        var (matchBody, hasSkippedNestedFields) = BuildMatchBody(request.Body, request.Headers);
        var hasXMatch = request.Query is { Count: > 0 } || matchHeaders.Count > 0 || matchBody is not null;
        var contentType = DetectContentType(request.Headers);

        var sb = new StringBuilder();
        sb.AppendLine("openapi: 3.1.0");
        sb.AppendLine("info:");
        sb.AppendLine("  title: Draft Stub");
        sb.AppendLine("  version: 0.0.0");
        sb.AppendLine("paths:");
        sb.AppendLine($"  {request.Path}:");
        sb.AppendLine($"    {method}:");
        sb.AppendLine($"      operationId: {operationId}");

        if (hasXMatch)
        {
            sb.AppendLine("      x-match:");
            sb.Append("        -");

            var firstCondition = true;

            if (request.Query is { Count: > 0 })
            {
                AppendConditionKey(sb, "query", ref firstCondition);
                foreach (var (key, values) in request.Query.OrderBy(q => q.Key, StringComparer.Ordinal))
                {
                    if (values.Length == 1)
                    {
                        sb.AppendLine($"            {key}: {YamlScalar(values[0])}");
                    }
                    else
                    {
                        sb.AppendLine($"            {key}:");
                        foreach (var v in values)
                        {
                            sb.AppendLine($"              - {YamlScalar(v)}");
                        }
                    }
                }
            }

            if (matchHeaders.Count > 0)
            {
                AppendConditionKey(sb, "headers", ref firstCondition);
                foreach (var (key, value) in matchHeaders.OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"            {key}: {YamlScalar(value)}");
                }
            }

            if (matchBody is not null)
            {
                AppendConditionKey(sb, "body", ref firstCondition);
                foreach (var (key, value) in matchBody.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    sb.AppendLine($"            {key}: {YamlScalar(value)}");
                }
                if (hasSkippedNestedFields)
                {
                    sb.AppendLine("            # TODO: nested fields were skipped — add them manually");
                }
            }

            sb.AppendLine("          response:");
            sb.AppendLine("            statusCode: 200");
            sb.AppendLine("            content:");
            sb.AppendLine("              application/json:");
            sb.AppendLine("                example: {}");
        }

        if (!string.IsNullOrEmpty(request.Body) && contentType is not null)
        {
            sb.AppendLine("      requestBody:");
            sb.AppendLine("        content:");
            sb.AppendLine($"          {contentType}:");
            sb.AppendLine("            example: {}");
        }

        sb.AppendLine("      responses:");
        sb.AppendLine("        '200':");
        sb.AppendLine("          description: TODO");
        sb.AppendLine("          content:");
        sb.AppendLine("            application/json:");
        sb.Append("              example: {}");

        return sb.ToString();
    }

    private static void AppendConditionKey(StringBuilder sb, string key, ref bool firstCondition)
    {
        if (firstCondition)
        {
            sb.AppendLine($" {key}:");
            firstCondition = false;
        }
        else
        {
            sb.AppendLine($"          {key}:");
        }
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
        if (contentType is null || !contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
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

            return result.Count > 0 ? (result, skipped) : (null, false);
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

    private static string YamlScalar(string value)
    {
        if (value.Length == 0)
        {
            return "''";
        }

        // Quote if value contains characters that would break YAML parsing.
        if (value.Contains(':') || value.Contains('#') || value.Contains('\'')
            || value.StartsWith('{') || value.StartsWith('[') || value.StartsWith('"')
            || value == "true" || value == "false"
            || value == "null" || value.StartsWith(' ') || value.EndsWith(' '))
        {
            // YAML single-quoted scalars escape apostrophes by doubling them.
            return $"'{value.Replace("'", "''")}'";
        }

        return value;
    }
}
