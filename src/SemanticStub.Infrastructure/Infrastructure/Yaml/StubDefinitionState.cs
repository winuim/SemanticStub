using Microsoft.Extensions.Logging;
using SemanticStub.Application.Infrastructure.Yaml;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services;

namespace SemanticStub.Infrastructure.Yaml;

/// <summary>
/// Holds the current process-wide YAML definition snapshot and swaps it atomically during reloads.
/// </summary>
public sealed class StubDefinitionState
{
    private readonly IStubDefinitionLoader _loader;
    private readonly ScenarioService _scenarioService;
    private readonly ILogger<StubDefinitionState> _logger;
    private readonly object _syncRoot = new();
    private StubDocument _currentDocument;

    /// <summary>
    /// Initializes the process-wide definition state from the default YAML definition.
    /// </summary>
    /// <param name="loader">The YAML definition loader used for initial load, reload, and response file content.</param>
    /// <param name="scenarioService">The scenario state service reset when definitions are reloaded.</param>
    /// <param name="logger">The logger used for reload diagnostics.</param>
    public StubDefinitionState(IStubDefinitionLoader loader, ScenarioService scenarioService, ILogger<StubDefinitionState> logger)
    {
        _loader = loader;
        _scenarioService = scenarioService;
        _logger = logger;
        _currentDocument = loader.LoadDefaultDefinition();
    }

    /// <summary>
    /// Returns the currently active stub document snapshot.
    /// </summary>
    public StubDocument GetCurrentDocument()
    {
        return Volatile.Read(ref _currentDocument);
    }

    /// <summary>
    /// Loads response file content relative to the configured YAML definition root.
    /// </summary>
    /// <param name="fileName">The response file name referenced from YAML.</param>
    public string LoadResponseFileContent(string fileName)
    {
        return _loader.LoadResponseFileContent(fileName);
    }

    /// <summary>
    /// Attempts to reload YAML definitions and keeps the previous snapshot if reload fails.
    /// </summary>
    /// <returns><see langword="true"/> when reload succeeds; otherwise, <see langword="false"/>.</returns>
    public bool TryReload()
    {
        lock (_syncRoot)
        {
            try
            {
                ApplyReloadedDocument(_loader.LoadDefaultDefinition());
                _logger.LogInformation("Reloaded stub definitions from disk.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload stub definitions. Continuing with the last successfully loaded definitions.");
                return false;
            }
        }
    }

    private void ApplyReloadedDocument(StubDocument reloadedDocument)
    {
        _scenarioService.ExecuteLocked(() =>
        {
            Volatile.Write(ref _currentDocument, reloadedDocument);
            ResetScenarioStatesWithinLock(reloadedDocument);
            return 0;
        });
    }

    private void ResetScenarioStatesWithinLock(StubDocument document)
    {
        _scenarioService.ResetScenariosWithinLock(
            StubScenarioNameCollector.Collect(document),
            DateTimeOffset.UtcNow);
    }
}
