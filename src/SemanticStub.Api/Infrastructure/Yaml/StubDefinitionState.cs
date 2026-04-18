using SemanticStub.Api.Models;
using SemanticStub.Api.Services;

namespace SemanticStub.Api.Infrastructure.Yaml;

/// <summary>
/// Holds the current process-wide YAML definition snapshot and swaps it atomically during reloads.
/// </summary>
internal sealed class StubDefinitionState
{
    private readonly IStubDefinitionLoader loader;
    private readonly ScenarioService scenarioService;
    private readonly ILogger<StubDefinitionState> logger;
    private readonly object syncRoot = new();
    private StubDocument currentDocument;

    public StubDefinitionState(IStubDefinitionLoader loader, ScenarioService scenarioService, ILogger<StubDefinitionState> logger)
    {
        this.loader = loader;
        this.scenarioService = scenarioService;
        this.logger = logger;
        currentDocument = loader.LoadDefaultDefinition();
    }

    public StubDocument GetCurrentDocument()
    {
        return Volatile.Read(ref currentDocument);
    }

    public string LoadResponseFileContent(string fileName)
    {
        return loader.LoadResponseFileContent(fileName);
    }

    public bool TryReload()
    {
        lock (syncRoot)
        {
            try
            {
                ApplyReloadedDocument(loader.LoadDefaultDefinition());
                logger.LogInformation("Reloaded stub definitions from disk.");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to reload stub definitions. Continuing with the last successfully loaded definitions.");
                return false;
            }
        }
    }

    private void ApplyReloadedDocument(StubDocument reloadedDocument)
    {
        scenarioService.ExecuteLocked(() =>
        {
            Volatile.Write(ref currentDocument, reloadedDocument);
            ResetScenarioStatesWithinLock(reloadedDocument);
            return 0;
        });
    }

    private void ResetScenarioStatesWithinLock(StubDocument document)
    {
        scenarioService.ResetScenariosWithinLock(
            StubScenarioNameCollector.Collect(document),
            DateTimeOffset.UtcNow);
    }
}
