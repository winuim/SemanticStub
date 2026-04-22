namespace SemanticStub.Application.Infrastructure.Yaml;

/// <summary>
/// Exposes the active stub definition version so process-wide caches can invalidate after reload.
/// </summary>
public interface IStubDefinitionVersionProvider
{
    /// <summary>
    /// Gets the monotonic version of the currently active stub definition snapshot.
    /// </summary>
    long CurrentVersion { get; }
}
