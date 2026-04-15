using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

/// <summary>
/// Resolves incoming HTTP requests against the loaded stub document and returns either a concrete response contract or a reason no response can be produced.
/// </summary>
public interface IStubService
{
    /// <summary>
    /// Returns the HTTP methods currently configured for the supplied path so callers can emit protocol details such as the <c>Allow</c> header on <c>405 Method Not Allowed</c> responses.
    /// </summary>
    /// <param name="path">The absolute request path such as <c>/users</c>.</param>
    /// <returns>The configured methods for the resolved path, or an empty list when no path matches.</returns>
    IReadOnlyList<string> GetAllowedMethods(string path)
    {
        return Array.Empty<string>();
    }

    /// <summary>
    /// Resolves a response for callers that only need method and path matching.
    /// </summary>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>.</param>
    /// <returns>The same result contract as the full overload, with query, header, and body matching treated as unspecified.</returns>
    StubMatchResult TryGetResponse(string method, string path, out StubResponse? response)
    {
        return TryGetResponse(
            method,
            path,
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null,
            out response);
    }

    /// <summary>
    /// Resolves a response while considering query-based match conditions so more specific stubs can override broad defaults.
    /// </summary>
    /// <param name="query">Query parameters keyed by parameter name, including repeated values in request order.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>.</param>
    /// <returns>The same result contract as the full overload, with headers omitted and the body treated as unspecified.</returns>
    StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, StringValues> query, out StubResponse? response)
    {
        return TryGetResponse(
            method,
            path,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null,
            out response);
    }

    /// <summary>
    /// Resolves a response while considering query and body match conditions so structured request payloads can select a narrower stub.
    /// </summary>
    /// <param name="query">Query parameters keyed by parameter name, including repeated values in request order.</param>
    /// <param name="body">The request body used for JSON body matching. <see langword="null"/> means no body conditions can match.</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>.</param>
    /// <returns>The same result contract as the full overload, with headers omitted.</returns>
    StubMatchResult TryGetResponse(string method, string path, IReadOnlyDictionary<string, StringValues> query, string? body, out StubResponse? response)
    {
        return TryGetResponse(
            method,
            path,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body,
            out response);
    }

    /// <summary>
    /// Resolves the most specific stub response by evaluating path, method, query, headers, and body through the shared matching pipeline.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate. Values are interpreted with ASP.NET Core's standard verb helpers.</param>
    /// <param name="path">The request path to match. Callers are expected to pass the absolute request path such as <c>/users</c>.</param>
    /// <param name="query">The query-string values to evaluate. Pass an empty dictionary when the request has no query parameters.</param>
    /// <param name="headers">The request headers to evaluate. Pass an empty dictionary when header matching is not needed; callers should use case-insensitive keys for HTTP semantics.</param>
    /// <param name="body">The request body used for JSON body matching. <see langword="null"/> means "no body supplied".</param>
    /// <param name="response">Receives the assembled response only when the return value is <see cref="StubMatchResult.Matched"/>; otherwise <see langword="null"/>.</param>
    /// <returns>
    /// <see cref="StubMatchResult.Matched"/> when a stub response was assembled,
    /// <see cref="StubMatchResult.PathNotFound"/> when no path matches,
    /// <see cref="StubMatchResult.MethodNotAllowed"/> when the path exists but not for the supplied method,
    /// or <see cref="StubMatchResult.ResponseNotConfigured"/> when a route matches but no usable response can be built.
    /// </returns>
    /// <remarks>
    /// Selecting a response may advance in-memory scenario state when the matched YAML response defines <c>x-scenario.next</c>.
    /// Relative file-backed responses may require reading payload content from the configured loader, while absolute file-backed responses can be returned as streamable file paths without loader resolution.
    /// </remarks>
    StubMatchResult TryGetResponse(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        out StubResponse? response);

    /// <summary>
    /// Resolves the most specific stub response asynchronously, enabling semantic fallback matching that makes HTTP calls to an external embedding endpoint without blocking the request thread.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate.</param>
    /// <param name="path">The request path to match.</param>
    /// <param name="query">The query-string values to evaluate.</param>
    /// <param name="headers">The request headers to evaluate.</param>
    /// <param name="body">The request body used for JSON body matching.</param>
    /// <returns>A tuple of the match result and the assembled response (non-null only when <see cref="StubMatchResult.Matched"/>).</returns>
    async Task<(StubMatchResult Result, StubResponse? Response)> TryGetResponseAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        var dispatch = await DispatchAsync(method, path, query, headers, body);
        return (dispatch.Result, dispatch.Response);
    }

    /// <summary>
    /// Resolves the request and returns the captured explanation from the same evaluation used for real dispatch.
    /// </summary>
    /// <param name="method">The HTTP method to evaluate.</param>
    /// <param name="path">The request path to match.</param>
    /// <param name="query">The query-string values to evaluate.</param>
    /// <param name="headers">The request headers to evaluate.</param>
    /// <param name="body">The request body used for JSON body matching.</param>
    /// <returns>The dispatch result, including the assembled response and explanation captured from the same evaluation.</returns>
    Task<StubDispatchResult> DispatchAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body);

    /// <summary>
    /// Explains how the runtime would evaluate the supplied virtual request without executing a response or mutating scenario state.
    /// </summary>
    /// <param name="request">The virtual request to evaluate.</param>
    /// <returns>The explanation produced by the shared matching pipeline.</returns>
    Task<MatchExplanationInfo> ExplainMatchAsync(MatchRequestInfo request);
}
