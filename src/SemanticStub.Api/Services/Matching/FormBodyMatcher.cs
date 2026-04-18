using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

internal sealed class FormBodyMatcher
{
    private const string FormUrlEncodedMediaType = "application/x-www-form-urlencoded";
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(100);
    private readonly ILogger<FormBodyMatcher>? _logger;

    internal FormBodyMatcher(ILogger<FormBodyMatcher>? logger = null)
    {
        _logger = logger;
    }

    internal IReadOnlyDictionary<string, StringValues>? ParseRequestBody(string? body, string? contentType)
    {
        if (string.IsNullOrEmpty(body) || !IsFormUrlEncoded(contentType))
        {
            return null;
        }

        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var pair in body.Split('&'))
        {
            if (pair.Length == 0)
            {
                continue;
            }

            var separatorIndex = pair.IndexOf('=');
            var key = separatorIndex < 0 ? pair : pair[..separatorIndex];
            var value = separatorIndex < 0 ? string.Empty : pair[(separatorIndex + 1)..];

            if (!TryDecode(key, out var decodedKey) ||
                !TryDecode(value, out var decodedValue))
            {
                return null;
            }

            if (!values.TryGetValue(decodedKey, out var currentValues))
            {
                currentValues = [];
                values[decodedKey] = currentValues;
            }

            currentValues.Add(decodedValue);
        }

        return values.ToDictionary(
            entry => entry.Key,
            entry => new StringValues(entry.Value.ToArray()),
            StringComparer.Ordinal);
    }

    internal bool IsMatch(object? expectedBody, IReadOnlyDictionary<string, StringValues>? actualForm)
    {
        if (!TryGetExpectedForm(expectedBody, out var expectedForm))
        {
            return false;
        }

        if (actualForm is null)
        {
            return false;
        }

        foreach (var expectedValue in expectedForm)
        {
            if (!actualForm.TryGetValue(expectedValue.Key, out var actualValue) ||
                !IsFormFieldMatch(expectedValue.Value, actualValue))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsFormFieldMatch(object? expected, StringValues actual)
    {
        if (MatchOperatorDefinition.TryGetEquals(expected, out var equals))
        {
            return IsExactStringMatch(equals, actual);
        }

        return MatchOperatorDefinition.TryGetRegex(expected, out var regex) &&
               IsRegexStringMatch(regex, actual);
    }

    internal bool HasFormCondition(object? expectedBody)
    {
        return TryGetExpectedForm(expectedBody, out _);
    }

    private static bool TryGetExpectedForm(object? expectedBody, out IReadOnlyDictionary<string, object?> expectedForm)
    {
        if (TryGetMap(expectedBody, out var bodyMap) &&
            bodyMap.TryGetValue("form", out var formValue) &&
            TryGetMap(formValue, out var formMap))
        {
            expectedForm = formMap;
            return true;
        }

        expectedForm = new Dictionary<string, object?>(StringComparer.Ordinal);
        return false;
    }

    private static bool IsExactStringMatch(object? expected, StringValues actual)
    {
        if (expected is IEnumerable expectedSequence and not string)
        {
            var expectedValues = expectedSequence.Cast<object?>().Select(ConvertFormValueToString).ToArray();
            var actualValues = actual.ToArray();

            return expectedValues.Length == actualValues.Length &&
                   expectedValues.SequenceEqual(actualValues, StringComparer.Ordinal);
        }

        return actual.Count == 1 &&
               actual[0] is not null &&
               string.Equals(ConvertFormValueToString(expected), actual[0], StringComparison.Ordinal);
    }

    private bool IsRegexStringMatch(object? expected, StringValues actual)
    {
        if (expected is IEnumerable expectedSequence and not string)
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
                    !IsSingleRegexStringMatch(expectedValues[index], actualValues[index]!))
                {
                    return false;
                }
            }

            return true;
        }

        return actual.Count == 1 &&
               actual[0] is not null &&
               IsSingleRegexStringMatch(expected, actual[0]!);
    }

    private bool IsSingleRegexStringMatch(object? expected, string actual)
    {
        if (expected is not string pattern)
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(actual, pattern, RegexOptions.CultureInvariant, RegexMatchTimeout);
        }
        catch (ArgumentException ex)
        {
            _logger?.LogWarning(ex, "Invalid regex match pattern '{Pattern}' in stub definition — treating as non-match.", pattern);
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            _logger?.LogWarning("Regex match pattern '{Pattern}' timed out after {TimeoutMs}ms — treating as non-match.", pattern, RegexMatchTimeout.TotalMilliseconds);
            return false;
        }
    }

    private static string ConvertFormValueToString(object? value)
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

    private static bool TryGetMap(object? value, out Dictionary<string, object?> map)
    {
        switch (value)
        {
            case IDictionary<string, object?> typed:
                map = new Dictionary<string, object?>(typed, StringComparer.Ordinal);
                return true;
            case IDictionary dictionary:
                map = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dictionary)
                {
                    map[entry.Key.ToString() ?? string.Empty] = entry.Value;
                }

                return true;
            default:
                map = [];
                return false;
        }
    }

    private static bool IsFormUrlEncoded(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var mediaType = contentType.Split(';', count: 2)[0].Trim();
        return string.Equals(mediaType, FormUrlEncodedMediaType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryDecode(string value, out string decoded)
    {
        try
        {
            decoded = Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));
            return true;
        }
        catch (UriFormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }
}
