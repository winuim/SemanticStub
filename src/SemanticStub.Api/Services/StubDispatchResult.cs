using SemanticStub.Api.Inspection;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

/// <summary>
/// Describes the result of resolving a real request, including the response contract and explanation captured from the same evaluation.
/// </summary>
public sealed class StubDispatchResult
{
    /// <summary>
    /// Gets the match result for the request.
    /// </summary>
    public StubMatchResult Result { get; init; }

    /// <summary>
    /// Gets the assembled response when the request matched a usable stub.
    /// </summary>
    public StubResponse? Response { get; init; }

    /// <summary>
    /// Gets the explanation captured from the same evaluation that produced the response result.
    /// </summary>
    public MatchExplanationInfo Explanation { get; init; } = new();
}
