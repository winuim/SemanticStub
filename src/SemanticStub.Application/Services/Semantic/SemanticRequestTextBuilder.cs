using System.Text;
using Microsoft.Extensions.Primitives;

namespace SemanticStub.Application.Services.Semantic;

/// <summary>
/// Builds the normalized request text used for semantic matching.
/// </summary>
internal static class SemanticRequestTextBuilder
{
    /// <summary>
    /// Builds the semantic request text from the incoming request parts.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The request path.</param>
    /// <param name="query">The query parameters.</param>
    /// <param name="headers">The request headers.</param>
    /// <param name="body">The request body.</param>
    /// <returns>The normalized request text sent to the embedding endpoint.</returns>
    public static string Build(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        var builder = new StringBuilder();
        builder.Append("method: ").Append(method.ToUpperInvariant()).AppendLine();
        builder.Append("path: ").Append(path).AppendLine();

        if (query.Count > 0)
        {
            builder.AppendLine("query:");

            foreach (var pair in query.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                builder.Append("  ")
                    .Append(pair.Key)
                    .Append(": ")
                    .AppendLine(string.Join(", ", pair.Value.Select(value => value ?? string.Empty)));
            }
        }

        if (headers.Count > 0)
        {
            builder.AppendLine("headers:");

            foreach (var pair in headers.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append("  ")
                    .Append(pair.Key)
                    .Append(": ")
                    .AppendLine(pair.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            builder.AppendLine("body:");
            builder.Append(body.Trim());
        }

        return builder.ToString();
    }
}
