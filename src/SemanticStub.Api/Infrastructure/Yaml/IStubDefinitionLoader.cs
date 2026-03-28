using SemanticStub.Api.Models;

namespace SemanticStub.Api.Infrastructure.Yaml;

/// <summary>
/// Loads validated OpenAPI-based stub definitions and file-backed response payloads from disk without exposing discovery, validation, or normalization internals to callers.
/// </summary>
public interface IStubDefinitionLoader
{
    /// <summary>
    /// Loads the active stub document from the configured definitions location.
    /// </summary>
    /// <returns>A validated and normalized <see cref="StubDocument"/> ready for runtime matching. Relative <c>x-response-file</c> values are resolved during normalization.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when an explicit configured definitions directory cannot be located.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the default definitions directory or stub files cannot be found from the current search root.</exception>
    /// <exception cref="InvalidOperationException">Thrown when YAML cannot be deserialized or does not satisfy the repository's OpenAPI and extension rules.</exception>
    StubDocument LoadDefaultDefinition();

    /// <summary>
    /// Loads the content for a file-backed response payload referenced by a stub definition.
    /// </summary>
    /// <param name="fileName">An absolute path produced during normalization, or a path relative to the active definitions directory.</param>
    /// <returns>The file contents exactly as stored on disk.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when resolving a relative path requires a configured definitions directory that cannot be located.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the requested file cannot be resolved from the active definitions directory.</exception>
    string LoadResponseFileContent(string fileName);
}
