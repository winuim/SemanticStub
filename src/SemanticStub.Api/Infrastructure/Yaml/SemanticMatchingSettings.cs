namespace SemanticStub.Api.Infrastructure.Yaml;

/// <summary>
/// Configures the optional semantic request matching fallback.
/// </summary>
public sealed class SemanticMatchingSettings
{
    /// <summary>
    /// Gets whether semantic matching is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the base URL of the Text Embeddings Inference endpoint.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Gets the minimum cosine similarity required to accept a semantic match.
    /// </summary>
    public double Threshold { get; init; } = 0.8d;

    /// <summary>
    /// Gets the minimum score gap required between the top two candidates to accept the highest-scoring semantic match.
    /// </summary>
    public double TopScoreMargin { get; init; }

    /// <summary>
    /// Gets the timeout in seconds applied to each embedding endpoint HTTP request. Defaults to 30 seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;
}
