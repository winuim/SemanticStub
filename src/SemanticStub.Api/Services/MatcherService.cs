using System.Collections;
using System.Globalization;
using System.Text.Json;
using SemanticStub.Api.Models;
using SemanticStub.Api.Utilities;

namespace SemanticStub.Api.Services;

/// <summary>
/// Chooses the most specific conditional match so YAML-defined query, header, and body refinements remain deterministic.
/// </summary>
public sealed class MatcherService
{
    /// <summary>
    /// Preserves the existing query-and-body matching entry point while delegating to the full matcher implementation.
    /// </summary>
    public QueryMatchDefinition? FindBestMatch(
        OperationDefinition operation,
        IReadOnlyDictionary<string, string> query,
        string? body)
    {
        return FindBestMatch(
            operation,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body);
    }

    /// <summary>
    /// Filters candidates by every configured condition and prefers the most constrained definition so explicit matches win over broad fallbacks.
    /// </summary>
    /// <returns>The best matching conditional definition, or <see langword="null"/> when none satisfy the request.</returns>
    public QueryMatchDefinition? FindBestMatch(
        OperationDefinition operation,
        IReadOnlyDictionary<string, string> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        return FindBestMatch(
            operation.Parameters,
            operation,
            query,
            headers,
            body);
    }

    /// <summary>
    /// Filters candidates by every configured condition and prefers the most constrained definition so explicit matches win over broad fallbacks.
    /// </summary>
    /// <returns>The best matching conditional definition, or <see langword="null"/> when none satisfy the request.</returns>
    public QueryMatchDefinition? FindBestMatch(
        IReadOnlyCollection<ParameterDefinition> pathParameters,
        OperationDefinition operation,
        IReadOnlyDictionary<string, string> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        if (operation.Matches.Count == 0)
        {
            return null;
        }

        using var bodyDocument = ParseRequestBody(body);
        var queryParameterTypes = BuildQueryParameterTypes(pathParameters, operation.Parameters);

        // Match conditions are conjunctive; after filtering, prefer the candidate with the most explicit constraints.
        return operation.Matches
            .Where(candidate => IsExactQueryMatch(candidate.Query, query, queryParameterTypes))
            .Where(candidate => IsExactHeaderMatch(candidate.Headers, headers))
            .Where(candidate => IsBodyMatch(candidate.Body, bodyDocument?.RootElement))
            .OrderByDescending(GetMatchSpecificity)
            .FirstOrDefault();
    }

    private static Dictionary<string, string> BuildQueryParameterTypes(
        IReadOnlyCollection<ParameterDefinition> pathParameters,
        IReadOnlyCollection<ParameterDefinition> operationParameters)
    {
        var queryParameterTypes = new Dictionary<string, string>(StringComparer.Ordinal);

        AddQueryParameterTypes(pathParameters, queryParameterTypes);
        AddQueryParameterTypes(operationParameters, queryParameterTypes);

        return queryParameterTypes;
    }

    private static void AddQueryParameterTypes(
        IReadOnlyCollection<ParameterDefinition> parameters,
        IDictionary<string, string> queryParameterTypes)
    {
        foreach (var parameter in parameters)
        {
            if (!string.Equals(parameter.In, "query", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(parameter.Name) ||
                string.IsNullOrWhiteSpace(parameter.Schema?.Type))
            {
                continue;
            }

            queryParameterTypes[parameter.Name] = parameter.Schema.Type;
        }
    }

    private static bool IsExactQueryMatch(
        IReadOnlyDictionary<string, object?> expected,
        IReadOnlyDictionary<string, string> actual,
        IReadOnlyDictionary<string, string> queryParameterTypes)
    {
        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var value) ||
                !IsTypedQueryValueMatch(pair.Value, value, queryParameterTypes.GetValueOrDefault(pair.Key)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTypedQueryValueMatch(object? expected, string actual, string? parameterType)
    {
        if (string.Equals(parameterType, "integer", StringComparison.OrdinalIgnoreCase))
        {
            return TryConvertInteger(expected, out var expectedInteger) &&
                   TryConvertInteger(actual, out var actualInteger) &&
                   expectedInteger == actualInteger;
        }

        if (string.Equals(parameterType, "number", StringComparison.OrdinalIgnoreCase))
        {
            return TryConvertNumber(expected, out var expectedNumber) &&
                   TryConvertNumber(actual, out var actualNumber) &&
                   expectedNumber == actualNumber;
        }

        if (string.Equals(parameterType, "boolean", StringComparison.OrdinalIgnoreCase))
        {
            return TryConvertBoolean(expected, out var expectedBoolean) &&
                   TryConvertBoolean(actual, out var actualBoolean) &&
                   expectedBoolean == actualBoolean;
        }

        return string.Equals(ConvertQueryValueToString(expected), actual, StringComparison.Ordinal);
    }

    private static bool TryConvertInteger(object? value, out decimal integer)
    {
        if (TryConvertNumber(value, out integer))
        {
            return decimal.Truncate(integer) == integer;
        }

        integer = default;
        return false;
    }

    private static bool TryConvertNumber(object? value, out decimal number)
    {
        switch (value)
        {
            case byte byteValue:
                number = byteValue;
                return true;
            case sbyte sbyteValue:
                number = sbyteValue;
                return true;
            case short shortValue:
                number = shortValue;
                return true;
            case ushort ushortValue:
                number = ushortValue;
                return true;
            case int intValue:
                number = intValue;
                return true;
            case uint uintValue:
                number = uintValue;
                return true;
            case long longValue:
                number = longValue;
                return true;
            case ulong ulongValue:
                return decimal.TryParse(ulongValue.ToString(CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
            case float floatValue:
                return decimal.TryParse(floatValue.ToString(CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
            case double doubleValue:
                return decimal.TryParse(doubleValue.ToString(CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
            case decimal decimalValue:
                number = decimalValue;
                return true;
            case string text:
                return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
            default:
                number = default;
                return false;
        }
    }

    private static bool TryConvertBoolean(object? value, out bool boolean)
    {
        switch (value)
        {
            case bool boolValue:
                boolean = boolValue;
                return true;
            case string text:
                return bool.TryParse(text, out boolean);
            default:
                boolean = default;
                return false;
        }
    }

    private static string ConvertQueryValueToString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            bool boolean => boolean ? "true" : "false",
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
                => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            IFormattable formattable => formattable.ToString(format: null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static bool IsExactHeaderMatch(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
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
            // Invalid JSON should behave like "no structured body match" rather than failing the whole request.
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

        var expectedJson = StubExampleSerializer.Serialize(expectedBody);
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

    private static int GetMatchSpecificity(QueryMatchDefinition match)
    {
        return match.Query.Count + match.Headers.Count + GetBodySpecificity(match.Body);
    }

    private static int GetBodySpecificity(object? body)
    {
        // Nested body shapes should outrank shallower ones so more concrete matches win.
        return body switch
        {
            null => 0,
            IDictionary dictionary => dictionary.Count + dictionary.Values.Cast<object?>().Sum(GetBodySpecificity),
            IEnumerable list when body is not string => list.Cast<object?>().Sum(GetBodySpecificity),
            _ => 1
        };
    }
}
