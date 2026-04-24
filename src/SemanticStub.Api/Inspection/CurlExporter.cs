using System.Text;
using System.Web;

namespace SemanticStub.Api.Inspection;

/// <summary>
/// Formats a <see cref="RecentRequestInfo"/> as a runnable curl command.
/// </summary>
public static class CurlExporter
{
    private static readonly HashSet<string> _skippedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host",
        "Connection",
        "Content-Length",
        "Transfer-Encoding",
    };

    /// <summary>
    /// Exports a recorded request as a curl command string.
    /// </summary>
    /// <param name="request">The recorded request to export.</param>
    /// <param name="baseUrl">The base URL (scheme + host) to prepend to the request path.</param>
    /// <returns>A single curl command that reproduces the recorded request.</returns>
    public static string Export(RecentRequestInfo request, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(baseUrl);

        var sb = new StringBuilder();
        sb.Append("curl -X ");
        sb.Append(request.Method.ToUpperInvariant());
        sb.Append(" '");
        sb.Append(EscapeSingleQuote(BuildUrl(baseUrl, request.Path, request.Query)));
        sb.Append('\'');

        if (request.Headers is { Count: > 0 })
        {
            foreach (var (key, value) in request.Headers.OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (_skippedHeaders.Contains(key))
                {
                    continue;
                }

                sb.Append(" \\\n  -H '");
                sb.Append(EscapeSingleQuote($"{key}: {value}"));
                sb.Append('\'');
            }
        }

        if (!string.IsNullOrEmpty(request.Body))
        {
            sb.Append(" \\\n  --data-raw '");
            sb.Append(EscapeSingleQuote(request.Body));
            sb.Append('\'');
        }

        return sb.ToString();
    }

    private static string BuildUrl(string baseUrl, string path, IReadOnlyDictionary<string, string[]>? query)
    {
        if (query is null || query.Count == 0)
        {
            return baseUrl + path;
        }

        var queryString = string.Join("&", query
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .SelectMany(p => p.Value.Select(v => $"{HttpUtility.UrlEncode(p.Key)}={HttpUtility.UrlEncode(v)}")));

        return $"{baseUrl}{path}?{queryString}";
    }

    private static string EscapeSingleQuote(string value) => value.Replace("'", "'\\''");
}
