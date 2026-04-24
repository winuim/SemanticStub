using System.Text.Json;
using Microsoft.Extensions.Logging;
using SemanticStub.Application.Utilities;

namespace SemanticStub.Application.Services;

/// <summary>
/// Parses request bodies and compares structured JSON body expectations without changing matcher orchestration behavior.
/// </summary>
internal sealed class JsonBodyMatcher
{
    private const int MaxMismatchCount = 10;
    private readonly ILogger<JsonBodyMatcher>? _logger;

    /// <summary>
    /// Creates a body matcher with optional warning logging for invalid stub body definitions.
    /// </summary>
    public JsonBodyMatcher(ILogger<JsonBodyMatcher>? logger = null)
    {
        _logger = logger;
    }

    internal JsonDocument? ParseRequestBody(string? body)
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
            // Invalid JSON should behave like "no structured body match" rather than failing the whole request.
            return null;
        }
    }

    internal bool IsMatch(object? expectedBody, JsonElement? actualBody)
    {
        if (expectedBody is null)
        {
            return true;
        }

        if (actualBody is null)
        {
            return false;
        }

        try
        {
            var expectedJson = StubExampleSerializer.Serialize(expectedBody);
            using var expectedDocument = JsonDocument.Parse(expectedJson);

            return IsJsonMatch(expectedDocument.RootElement, actualBody.Value);
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Invalid body match definition in stub YAML — treating as non-match.");
            return false;
        }
        catch (NotSupportedException ex)
        {
            _logger?.LogWarning(ex, "Unsupported body match definition in stub YAML — treating as non-match.");
            return false;
        }
    }

    internal IReadOnlyList<MatchDimensionMismatch> CollectMismatches(object? expectedBody, JsonElement? actualBody)
    {
        if (expectedBody is null)
        {
            return [];
        }

        var mismatches = new List<MatchDimensionMismatch>();

        try
        {
            var expectedJson = StubExampleSerializer.Serialize(expectedBody);
            using var expectedDocument = JsonDocument.Parse(expectedJson);

            if (actualBody is null)
            {
                AddMismatch(mismatches, "$", FormatValue(expectedDocument.RootElement), null, "missing");
                return mismatches;
            }

            CollectJsonMismatches(expectedDocument.RootElement, actualBody.Value, "$", mismatches);
            return mismatches;
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Invalid body match definition in stub YAML — mismatch details unavailable.");
            return [];
        }
        catch (NotSupportedException ex)
        {
            _logger?.LogWarning(ex, "Unsupported body match definition in stub YAML — mismatch details unavailable.");
            return [];
        }
    }

    private static bool IsJsonMatch(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind == JsonValueKind.Object)
        {
            if (actual.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // Object matches are partial: every expected property must exist and match, but extra actual properties are allowed.
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

    private static void CollectJsonMismatches(
        JsonElement expected,
        JsonElement actual,
        string path,
        List<MatchDimensionMismatch> mismatches)
    {
        if (mismatches.Count >= MaxMismatchCount)
        {
            return;
        }

        if (expected.ValueKind == JsonValueKind.Object)
        {
            if (actual.ValueKind != JsonValueKind.Object)
            {
                AddMismatch(mismatches, path, FormatValue(expected), FormatValue(actual), "unequal");
                return;
            }

            foreach (var property in expected.EnumerateObject())
            {
                if (mismatches.Count >= MaxMismatchCount)
                {
                    return;
                }

                var propertyPath = AppendPropertyPath(path, property.Name);
                if (!actual.TryGetProperty(property.Name, out var actualProperty))
                {
                    AddMismatch(mismatches, propertyPath, FormatValue(property.Value), null, "missing");
                    continue;
                }

                CollectJsonMismatches(property.Value, actualProperty, propertyPath, mismatches);
            }

            return;
        }

        if (expected.ValueKind == JsonValueKind.Array)
        {
            if (actual.ValueKind != JsonValueKind.Array)
            {
                AddMismatch(mismatches, path, FormatValue(expected), FormatValue(actual), "unequal");
                return;
            }

            var expectedItems = expected.EnumerateArray().ToArray();
            var actualItems = actual.EnumerateArray().ToArray();

            if (expectedItems.Length != actualItems.Length)
            {
                AddMismatch(mismatches, path, FormatArrayLength(expectedItems.Length), FormatArrayLength(actualItems.Length), "unequal");
                return;
            }

            for (var index = 0; index < expectedItems.Length; index++)
            {
                if (mismatches.Count >= MaxMismatchCount)
                {
                    return;
                }

                CollectJsonMismatches(expectedItems[index], actualItems[index], $"{path}[{index}]", mismatches);
            }

            return;
        }

        if (expected.ValueKind != actual.ValueKind || !ScalarValuesMatch(expected, actual))
        {
            AddMismatch(mismatches, path, FormatValue(expected), FormatValue(actual), "unequal");
        }
    }

    private static bool ScalarValuesMatch(JsonElement expected, JsonElement actual)
    {
        return expected.ValueKind switch
        {
            JsonValueKind.String => expected.GetString() == actual.GetString(),
            JsonValueKind.Number => expected.GetRawText() == actual.GetRawText(),
            JsonValueKind.True or JsonValueKind.False => expected.GetBoolean() == actual.GetBoolean(),
            JsonValueKind.Null => true,
            _ => expected.GetRawText() == actual.GetRawText()
        };
    }

    private static void AddMismatch(
        List<MatchDimensionMismatch> mismatches,
        string path,
        string? expected,
        string? actual,
        string kind)
    {
        if (mismatches.Count >= MaxMismatchCount)
        {
            return;
        }

        mismatches.Add(new MatchDimensionMismatch
        {
            Dimension = "body",
            Key = path,
            Expected = expected,
            Actual = actual,
            Kind = kind,
        });
    }

    private static string AppendPropertyPath(string path, string propertyName)
    {
        return IsSimplePathSegment(propertyName)
            ? $"{path}.{propertyName}"
            : $"{path}['{propertyName.Replace("'", "\\'", StringComparison.Ordinal)}']";
    }

    private static bool IsSimplePathSegment(string value)
    {
        if (string.IsNullOrEmpty(value) || !(char.IsLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }

        return value.All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    private static string FormatArrayLength(int length)
    {
        return length == 1 ? "1 item" : $"{length} items";
    }

    private static string? FormatValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.Object => "object",
            JsonValueKind.Array => "array",
            _ => value.GetRawText(),
        };
    }
}
