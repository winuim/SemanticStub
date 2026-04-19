using System.Collections;

namespace SemanticStub.Application.Models;

/// <summary>
/// Defines supported structured match operators used by YAML validation and request matching.
/// </summary>
public static class MatchOperatorDefinition
{
    /// <summary>
    /// The operator key for exact value comparisons.
    /// </summary>
    public const string EqualsOperator = "equals";

    /// <summary>
    /// The operator key for regular expression comparisons.
    /// </summary>
    public const string RegexOperator = "regex";

    /// <summary>
    /// Returns whether the supplied value is a map containing at least one supported match operator.
    /// </summary>
    /// <param name="value">The YAML value to inspect.</param>
    public static bool IsOperatorMap(object? value)
        => TryGetMap(value, out var map) &&
           (map.ContainsKey(EqualsOperator) || map.ContainsKey(RegexOperator));

    /// <summary>
    /// Attempts to extract the exact comparison value from a structured operator map or raw value.
    /// </summary>
    /// <param name="value">The YAML value to inspect.</param>
    /// <param name="equals">The extracted exact comparison value.</param>
    public static bool TryGetEquals(object? value, out object? equals)
    {
        if (TryGetMap(value, out var map) && map.TryGetValue(EqualsOperator, out equals))
        {
            return true;
        }

        if (IsOperatorMap(value))
        {
            equals = null;
            return false;
        }

        equals = value;
        return true;
    }

    /// <summary>
    /// Attempts to extract the regular expression comparison value from a structured operator map.
    /// </summary>
    /// <param name="value">The YAML value to inspect.</param>
    /// <param name="regex">The extracted regular expression comparison value.</param>
    public static bool TryGetRegex(object? value, out object? regex)
    {
        if (TryGetMap(value, out var map) && map.TryGetValue(RegexOperator, out regex))
        {
            return true;
        }

        regex = null;
        return false;
    }

    /// <summary>
    /// Returns all keys from a structured operator map, including unsupported keys for validation diagnostics.
    /// </summary>
    /// <param name="value">The YAML value to inspect.</param>
    public static IReadOnlyCollection<string> GetKeys(object? value)
    {
        if (!TryGetMap(value, out var map))
        {
            return [];
        }

        return map.Keys;
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
}
