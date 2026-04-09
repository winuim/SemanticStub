using System.Collections;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace SemanticStub.Api.Services;

/// <summary>
/// Evaluates <c>x-query-regex</c> constraints without changing matcher orchestration or precedence behavior.
/// </summary>
internal sealed class RegexQueryMatcher
{
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(100);
    private readonly ILogger<RegexQueryMatcher>? logger;

    /// <summary>
    /// Creates a regex query matcher with optional warning logging for invalid or slow regex patterns.
    /// </summary>
    internal RegexQueryMatcher(ILogger<RegexQueryMatcher>? logger = null)
    {
        this.logger = logger;
    }

    internal bool IsMatch(
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

    private bool IsRegexQueryValueMatch(object? expected, StringValues actual)
    {
        if (expected is IEnumerable expectedSequence && expected is not string)
        {
            return IsRegexQuerySequenceMatch(expectedSequence, actual);
        }

        return actual.Count == 1 &&
               actual[0] is not null &&
               IsSingleRegexQueryValueMatch(expected, actual[0]!);
    }

    private bool IsRegexQuerySequenceMatch(IEnumerable expectedSequence, StringValues actual)
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

    private bool IsSingleRegexQueryValueMatch(object? expected, string actual)
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
            logger?.LogWarning(ex, "Invalid x-regex-query pattern '{Pattern}' in stub definition — treating as non-match.", pattern);
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            logger?.LogWarning("x-regex-query pattern '{Pattern}' timed out after {TimeoutMs}ms — treating as non-match.", pattern, RegexMatchTimeout.TotalMilliseconds);
            return false;
        }
    }
}
