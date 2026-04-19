using System.Text.Json;
using Microsoft.Extensions.Logging;
using SemanticStub.Application.Utilities;

namespace SemanticStub.Application.Services;

/// <summary>
/// Parses request bodies and compares structured JSON body expectations without changing matcher orchestration behavior.
/// </summary>
internal sealed class JsonBodyMatcher
{
    private readonly ILogger<JsonBodyMatcher>? _logger;

    /// <summary>
    /// Creates a body matcher with optional warning logging for invalid stub body definitions.
    /// </summary>
    internal JsonBodyMatcher(ILogger<JsonBodyMatcher>? logger = null)
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
}
