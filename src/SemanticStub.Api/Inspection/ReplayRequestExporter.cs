namespace SemanticStub.Api.Inspection;

/// <summary>
/// Converts a <see cref="RecentRequestInfo"/> into a <see cref="ReplayReadyRequestInfo"/>
/// by retaining only the fields required for replay and dropping runtime metadata.
/// </summary>
public static class ReplayRequestExporter
{
    private static readonly HashSet<string> _skippedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host",
        "Connection",
        "Content-Length",
        "Transfer-Encoding",
    };

    /// <summary>
    /// Exports a recorded request as a replay-ready structured model.
    /// </summary>
    /// <param name="request">The recorded request to export.</param>
    /// <returns>
    /// A <see cref="ReplayReadyRequestInfo"/> containing the method, path, query, headers, and body
    /// needed to reproduce the request. Transport-only headers are omitted.
    /// </returns>
    public static ReplayReadyRequestInfo Export(RecentRequestInfo request)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyDictionary<string, string>? filteredHeaders = null;

        if (request.Headers is { Count: > 0 })
        {
            var dict = request.Headers
                .Where(h => !_skippedHeaders.Contains(h.Key))
                .ToDictionary(h => h.Key, h => h.Value, StringComparer.OrdinalIgnoreCase);

            if (dict.Count > 0)
            {
                filteredHeaders = dict;
            }
        }

        return new ReplayReadyRequestInfo
        {
            Method = request.Method.ToUpperInvariant(),
            Path = request.Path,
            Query = request.Query,
            Headers = filteredHeaders,
            Body = request.Body,
        };
    }
}
