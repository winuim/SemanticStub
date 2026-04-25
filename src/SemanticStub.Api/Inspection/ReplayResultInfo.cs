namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes the result of replaying a recorded request through the stub matching pipeline.
/// Contains the normalised replay request and the full match explanation produced by the dry run.
/// </summary>
public sealed class ReplayResultInfo
{
    /// <summary>
    /// Gets the request that was replayed.
    /// </summary>
    public required ReplayReadyRequestInfo Request { get; init; }

    /// <summary>
    /// Gets the match explanation produced by replaying the request.
    /// </summary>
    public required MatchExplanationInfo Explanation { get; init; }
}
