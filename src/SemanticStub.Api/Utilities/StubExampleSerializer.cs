using System.Text.Json;

namespace SemanticStub.Api.Utilities;

public static class StubExampleSerializer
{
    public static string Serialize(object? example)
    {
        var normalized = NormalizeValue(example);

        return JsonSerializer.Serialize(normalized);
    }

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
