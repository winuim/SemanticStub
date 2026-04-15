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
    IReadOnlyList<string> GetAllowedMethods(string path);

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
