using System.Collections;
using System.Globalization;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

/// <summary>
/// Evaluates exact query value matches, including typed comparisons for declared query parameter schemas.
/// </summary>
internal sealed class QueryValueMatcher
{
    /// <summary>
    /// Determines whether every expected query value matches exactly against the actual request query values.
    /// </summary>
    /// <param name="expected">Expected query values keyed by parameter name.</param>
    /// <param name="actual">Actual request query values keyed by parameter name.</param>
    /// <param name="queryParameterTypes">Declared OpenAPI query parameter types keyed by parameter name.</param>
    /// <returns><see langword="true"/> when every expected query value matches exactly; otherwise, <see langword="false"/>.</returns>
    public bool IsExactMatch(
        IReadOnlyDictionary<string, object?> expected,
        IReadOnlyDictionary<string, StringValues> actual,
        IReadOnlyDictionary<string, string> queryParameterTypes)
    {
        foreach (var pair in expected)
        {
            if (!MatchOperatorDefinition.TryGetEquals(pair.Value, out var expectedValue))
            {
                continue;
            }

            if (!actual.TryGetValue(pair.Key, out var value) ||
                !IsTypedQueryValueMatch(expectedValue, value, queryParameterTypes.GetValueOrDefault(pair.Key)))
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
}
