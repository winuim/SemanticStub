namespace SemanticStub.Infrastructure.Yaml;

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
    /// Values range from -1.0 (opposite) to 1.0 (identical). The default of 0.85
    /// favors safer matching and reduces broad false positives.
    /// </summary>
    public double Threshold { get; init; } = 0.85d;

    /// <summary>
    /// Gets the minimum score gap required between the top two candidates to accept
    /// the highest-scoring semantic match. When two candidates score within this
    /// margin of each other the match is treated as ambiguous and no candidate is
    /// selected. Defaults to 0, which disables the ambiguity check entirely.
    /// </summary>
    public double TopScoreMargin { get; init; }

    /// <summary>
    /// Gets the timeout in seconds applied to each embedding endpoint HTTP request. Defaults to 30 seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;
}
