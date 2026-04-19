using System.Text.Json;

namespace SemanticStub.Application.Utilities;

/// <summary>
/// Normalizes YAML-deserialized example values into JSON-friendly shapes so matching and response generation can compare values consistently.
/// </summary>
public static class StubExampleSerializer
{
    /// <summary>
    /// Serializes example data into canonical JSON so downstream matching logic is not coupled to YAML-specific runtime types.
    /// </summary>
    public static string Serialize(object? example)
    {
        var normalized = NormalizeValue(example);

        return JsonSerializer.Serialize(normalized);
    }

    /// <summary>
    /// Rewrites YAML-native collections into predictable CLR objects so serialization and header formatting produce stable results.
    /// </summary>
    public static object? NormalizeValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is IDictionary<object, object> dictionary)
        {
            var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var entry in dictionary)
            {
                normalized[entry.Key.ToString() ?? string.Empty] = NormalizeValue(entry.Value);
            }

            return normalized;
        }

        if (value is IEnumerable<object> list && value is not string)
        {
            return list.Select(NormalizeValue).ToList();
        }

        return value;
    }
}
