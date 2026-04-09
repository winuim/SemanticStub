using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;
using System.Collections;
using System.Globalization;

namespace SemanticStub.Api.Services;

internal static class StubResponseHeaderBuilder
{
    public static IReadOnlyDictionary<string, StringValues> BuildResponseHeaders(IReadOnlyDictionary<string, HeaderDefinition> headers)
    {
        if (headers.Count == 0)
        {
            return new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        }

        var resolvedHeaders = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            var resolvedValue = ResolveHeaderValue(header.Value);

            if (resolvedValue.Count == 0)
            {
                continue;
            }

            resolvedHeaders[header.Key] = string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase)
                ? resolvedValue
                : new StringValues(string.Join(", ", resolvedValue.ToArray().Where(static value => value is not null)!));
        }

        return resolvedHeaders;
    }

    private static StringValues ResolveHeaderValue(HeaderDefinition header)
    {
        return ConvertHeaderValueToStringValues(header.Example).Count > 0
            ? ConvertHeaderValueToStringValues(header.Example)
            : ConvertHeaderValueToStringValues(header.Schema?.Example);
    }

    private static StringValues ConvertHeaderValueToStringValues(object? value)
    {
        return value switch
        {
            null => StringValues.Empty,
            string text => new StringValues(text),
            char character => new StringValues(character.ToString()),
            bool boolean => new StringValues(boolean ? "true" : "false"),
            DateTime dateTime => new StringValues(dateTime.ToString("O", CultureInfo.InvariantCulture)),
            DateTimeOffset dateTimeOffset => new StringValues(dateTimeOffset.ToString("O", CultureInfo.InvariantCulture)),
            DateOnly dateOnly => new StringValues(dateOnly.ToString("O", CultureInfo.InvariantCulture)),
            TimeOnly timeOnly => new StringValues(timeOnly.ToString("O", CultureInfo.InvariantCulture)),
            Guid guid => new StringValues(guid.ToString()),
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
                => new StringValues(Convert.ToString(value, CultureInfo.InvariantCulture)),
            IFormattable formattable => new StringValues(formattable.ToString(format: null, CultureInfo.InvariantCulture)),
            IEnumerable sequence => ConvertHeaderSequenceToStringValues(sequence),
            _ => new StringValues(value.ToString())
        };
    }

    private static StringValues ConvertHeaderSequenceToStringValues(IEnumerable sequence)
    {
        var values = sequence
            .Cast<object?>()
            .SelectMany(static value => ConvertHeaderValueToStringValues(value).ToArray())
            .Where(static value => !string.IsNullOrEmpty(value))
            .ToArray();

        return values.Length == 0 ? StringValues.Empty : new StringValues(values);
    }
}
