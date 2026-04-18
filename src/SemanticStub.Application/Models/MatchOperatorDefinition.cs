using System.Collections;

namespace SemanticStub.Api.Models;

internal static class MatchOperatorDefinition
{
    public const string EqualsOperator = "equals";
    public const string RegexOperator = "regex";

    public static bool IsOperatorMap(object? value)
        => TryGetMap(value, out var map) &&
           (map.ContainsKey(EqualsOperator) || map.ContainsKey(RegexOperator));

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

    public static bool TryGetRegex(object? value, out object? regex)
    {
        if (TryGetMap(value, out var map) && map.TryGetValue(RegexOperator, out regex))
        {
            return true;
        }

        regex = null;
        return false;
    }

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
