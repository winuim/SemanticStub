using Microsoft.Extensions.Logging;
using SemanticStub.Application.Infrastructure.Yaml;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services;

namespace SemanticStub.Infrastructure.Yaml;

/// <summary>
/// Holds the current process-wide YAML definition snapshot and swaps it atomically during reloads.
/// </summary>
internal sealed class StubDefinitionState
{
    private readonly IStubDefinitionLoader _loader;
    private readonly ScenarioService _scenarioService;
    private readonly ILogger<StubDefinitionState> _logger;
    private readonly object _syncRoot = new();
    private StubDocument _currentDocument;

    public StubDefinitionState(IStubDefinitionLoader loader, ScenarioService scenarioService, ILogger<StubDefinitionState> logger)
    {
        _loader = loader;
        _scenarioService = scenarioService;
        _logger = logger;
        _currentDocument = loader.LoadDefaultDefinition();
    }

    public StubDocument GetCurrentDocument()
    {
        return Volatile.Read(ref _currentDocument);
    }

    public string LoadResponseFileContent(string fileName)
    {
        return _loader.LoadResponseFileContent(fileName);
    }

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
