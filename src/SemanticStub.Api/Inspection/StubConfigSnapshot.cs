namespace SemanticStub.Api.Inspection;

/// <summary>
/// A point-in-time snapshot of the active stub configuration.
/// </summary>
public sealed class StubConfigSnapshot
{
    /// <summary>Gets the UTC timestamp when this snapshot was taken.</summary>
    public required DateTimeOffset SnapshotTimestamp { get; init; }

    /// <summary>
    /// Gets a SHA-256 hash derived from the structural configuration of the loaded routes.
    /// </summary>
    /// <remarks>
    /// The hash covers path keys, HTTP methods, operation IDs, response status-code keys,
    /// conditional match counts, and semantic-match description strings. It changes whenever
    /// routes are added, removed, or structurally modified.
    ///
    /// Response body content, headers, delays, scenario definitions, and non-semantic
    /// query/body match criteria are intentionally excluded to avoid serialisation issues
    /// with dynamically-typed YAML fields. Use <see cref="SnapshotTimestamp"/> to detect
    /// any reload event, including changes to those excluded fields.
    /// </remarks>
    public required string ConfigurationHash { get; init; }

    /// <summary>Gets the absolute path of the directory from which stub definitions were loaded.</summary>
    public required string DefinitionsDirectoryPath { get; init; }

    /// <summary>Gets the total number of routes (path + method combinations) currently defined.</summary>
    public required int RouteCount { get; init; }

    /// <summary>Gets whether semantic matching is enabled in the current configuration.</summary>
    public required bool SemanticMatchingEnabled { get; init; }
}
