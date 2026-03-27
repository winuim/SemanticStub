using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;
using SemanticStub.Api.Utilities;

namespace SemanticStub.Api.Services;

/// <summary>
/// Chooses the most specific conditional match so YAML-defined query, header, and body refinements remain deterministic.
/// </summary>
public sealed class MatcherService
{
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(100);

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
            ConvertQueryValues(query),
            body);
    }

    /// <summary>
    /// Preserves the existing query-and-body matching entry point while delegating to the full matcher implementation.
    /// </summary>
    public QueryMatchDefinition? FindBestMatch(
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
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
            operation,
            ConvertQueryValues(query),
            headers,
            body);
    }

    /// <summary>
    /// Filters candidates by every configured condition and prefers the most constrained definition so explicit matches win over broad fallbacks.
    /// </summary>
    /// <returns>The best matching conditional definition, or <see langword="null"/> when none satisfy the request.</returns>
    public QueryMatchDefinition? FindBestMatch(
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
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
        IReadOnlyDictionary<string, StringValues> query,
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
        return GetCandidatesMatchingRequest(
                operation.Matches,
                query,
                headers,
                queryParameterTypes,
                bodyDocument?.RootElement)
            .OrderByDescending(GetExactQuerySpecificity)
            .ThenByDescending(GetMatchSpecificity)
            .ThenByDescending(GetRegexQuerySpecificity)
            .FirstOrDefault();
    }

    private static IEnumerable<QueryMatchDefinition> GetCandidatesMatchingRequest(
        IReadOnlyCollection<QueryMatchDefinition> candidates,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        IReadOnlyDictionary<string, string> queryParameterTypes,
        JsonElement? requestBody)
    {
        foreach (var candidate in candidates)
        {
            if (IsCandidateMatch(candidate, query, headers, queryParameterTypes, requestBody))
            {
                yield return candidate;
            }
        }
    }

    private static bool IsCandidateMatch(
        QueryMatchDefinition candidate,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        IReadOnlyDictionary<string, string> queryParameterTypes,
        JsonElement? requestBody)
    {
        return IsQueryMatch(candidate, query, queryParameterTypes) &&
               IsExactHeaderMatch(candidate.Headers, headers) &&
               IsBodyMatch(candidate.Body, requestBody);
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
        IReadOnlyDictionary<string, StringValues> actual,
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

    private static bool IsQueryMatch(
        QueryMatchDefinition match,
        IReadOnlyDictionary<string, StringValues> actual,
        IReadOnlyDictionary<string, string> queryParameterTypes)
    {
        return IsExactQueryMatch(match.Query, actual, queryParameterTypes) &&
               IsRegexQueryMatch(match.RegexQuery, actual) &&
               IsPartialQueryMatch(match.PartialQuery, actual, queryParameterTypes);
    }

    private static bool IsRegexQueryMatch(
        IReadOnlyDictionary<string, object?> expected,
        IReadOnlyDictionary<string, StringValues> actual)
    {
        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var value) || !IsRegexQueryValueMatch(pair.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPartialQueryMatch(
        IReadOnlyDictionary<string, object?> expected,
        IReadOnlyDictionary<string, StringValues> actual,
        IReadOnlyDictionary<string, string> queryParameterTypes)
    {
        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var value) ||
                !IsTypedPartialQueryValueMatch(pair.Value, value, queryParameterTypes.GetValueOrDefault(pair.Key)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTypedQueryValueMatch(object? expected, StringValues actual, string? parameterType)
    {
        if (expected is IEnumerable expectedSequence && expected is not string)
        {
            return IsTypedQuerySequenceMatch(expectedSequence, actual, parameterType);
        }

        return actual.Count == 1 &&
               actual[0] is not null &&
               IsTypedSingleQueryValueMatch(expected, actual[0]!, parameterType);
    }

    private static bool IsTypedPartialQueryValueMatch(object? expected, StringValues actual, string? parameterType)
    {
        if (expected is IEnumerable expectedSequence && expected is not string)
        {
            return IsTypedPartialQuerySequenceMatch(expectedSequence, actual, parameterType);
        }

        return actual.Any(actualValue =>
            actualValue is not null &&
            IsTypedSinglePartialQueryValueMatch(expected, actualValue!, parameterType));
    }

    private static bool IsRegexQueryValueMatch(object? expected, StringValues actual)
    {
        if (expected is IEnumerable expectedSequence && expected is not string)
        {
            return IsRegexQuerySequenceMatch(expectedSequence, actual);
        }

        return actual.Count == 1 &&
               actual[0] is not null &&
               IsSingleRegexQueryValueMatch(expected, actual[0]!);
    }

    private static bool IsRegexQuerySequenceMatch(IEnumerable expectedSequence, StringValues actual)
    {
        var expectedValues = expectedSequence.Cast<object?>().ToArray();
        var actualValues = actual.ToArray();

        if (expectedValues.Length != actualValues.Length)
        {
            return false;
        }

        for (var index = 0; index < expectedValues.Length; index++)
        {
            if (actualValues[index] is null ||
                !IsSingleRegexQueryValueMatch(expectedValues[index], actualValues[index]!))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSingleRegexQueryValueMatch(object? expected, string actual)
    {
        if (expected is not string pattern)
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(actual, pattern, RegexOptions.CultureInvariant, RegexMatchTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool IsTypedQuerySequenceMatch(IEnumerable expectedSequence, StringValues actual, string? parameterType)
    {
        var expectedValues = expectedSequence.Cast<object?>().ToArray();
        var actualValues = actual.ToArray();

        if (expectedValues.Length != actualValues.Length)
        {
            return false;
        }

        for (var index = 0; index < expectedValues.Length; index++)
        {
            if (actualValues[index] is null ||
                !IsTypedSingleQueryValueMatch(expectedValues[index], actualValues[index]!, parameterType))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTypedPartialQuerySequenceMatch(IEnumerable expectedSequence, StringValues actual, string? parameterType)
    {
        var expectedValues = expectedSequence.Cast<object?>().ToArray();
        var actualValues = actual.ToArray();

        if (expectedValues.Length == 0)
        {
            return true;
        }

        var actualIndex = 0;

        foreach (var expectedValue in expectedValues)
        {
            var matched = false;

            while (actualIndex < actualValues.Length)
            {
                var actualValue = actualValues[actualIndex++];

                if (actualValue is not null &&
                    IsTypedSinglePartialQueryValueMatch(expectedValue, actualValue!, parameterType))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTypedSingleQueryValueMatch(object? expected, string actual, string? parameterType)
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

    private static bool IsTypedSinglePartialQueryValueMatch(object? expected, string actual, string? parameterType)
    {
        if (string.Equals(parameterType, "integer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parameterType, "number", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parameterType, "boolean", StringComparison.OrdinalIgnoreCase))
        {
            return IsTypedSingleQueryValueMatch(expected, actual, parameterType);
        }

        var expectedText = ConvertQueryValueToString(expected);

        if (expectedText.Length == 0)
        {
            return string.Equals(expectedText, actual, StringComparison.Ordinal);
        }

        return actual.Contains(expectedText, StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, StringValues> ConvertQueryValues(IReadOnlyDictionary<string, string> query)
    {
        return query.ToDictionary(
            entry => entry.Key,
            entry => new StringValues(entry.Value),
            StringComparer.Ordinal);
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
        return match.Query.Count + match.RegexQuery.Count + match.PartialQuery.Count + match.Headers.Count + GetBodySpecificity(match.Body);
    }

    private static int GetExactQuerySpecificity(QueryMatchDefinition match)
    {
        return match.Query.Count;
    }

    private static int GetRegexQuerySpecificity(QueryMatchDefinition match)
    {
        return match.RegexQuery.Count;
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
