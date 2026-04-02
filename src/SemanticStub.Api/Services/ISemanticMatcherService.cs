using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

/// <summary>
/// Resolves an optional semantic fallback match for requests that did not satisfy deterministic matching rules.
/// </summary>
public interface ISemanticMatcherService
{
    /// <summary>
    /// Finds the best semantic match among the supplied conditional candidates.
    /// </summary>
    /// <param name="method">The HTTP method being evaluated.</param>
    /// <param name="path">The request path being evaluated.</param>
    /// <param name="query">The request query values keyed by parameter name.</param>
    /// <param name="headers">The request headers keyed by header name.</param>
    /// <param name="body">The optional request body.</param>
    /// <param name="candidates">The semantic candidates attached to the resolved operation.</param>
    /// <param name="candidateFilter">An optional filter that excludes ineligible candidates before scoring.</param>
    /// <returns>The best acceptable semantic match, or <see langword="null"/> when semantic matching is disabled or no candidate satisfies the threshold.</returns>
    QueryMatchDefinition? FindBestMatch(
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        IReadOnlyCollection<QueryMatchDefinition> candidates,
        Func<QueryMatchDefinition, bool>? candidateFilter = null);
}
