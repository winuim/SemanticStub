using SemanticStub.Api.Models;
using SemanticStub.Api.Services;

namespace SemanticStub.Api.Infrastructure.Yaml;

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
                var reloadedDocument = loader.LoadDefaultDefinition();
                scenarioService.ExecuteLocked(() =>
                {
                    Volatile.Write(ref currentDocument, reloadedDocument);
                    scenarioService.ResetScenariosWithinLock(GetScenarioNames(reloadedDocument), DateTimeOffset.UtcNow);
                    return 0;
                });
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

    private static IReadOnlyList<string> GetScenarioNames(StubDocument document)
    {
        var scenarioNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pathItem in document.Paths.Values)
        {
            AddScenarioNames(pathItem.Get, scenarioNames);
            AddScenarioNames(pathItem.Post, scenarioNames);
            AddScenarioNames(pathItem.Put, scenarioNames);
            AddScenarioNames(pathItem.Patch, scenarioNames);
            AddScenarioNames(pathItem.Delete, scenarioNames);
        }

        return scenarioNames.ToList();
    }

    private static void AddScenarioNames(OperationDefinition? operation, ISet<string> scenarioNames)
    {
        if (operation is null)
        {
            return;
        }

        foreach (var response in operation.Responses.Values)
        {
            if (response.Scenario is not null)
            {
                scenarioNames.Add(response.Scenario.Name);
            }
        }

        foreach (var match in operation.Matches)
        {
            if (match.Response.Scenario is not null)
            {
                scenarioNames.Add(match.Response.Scenario.Name);
            }
        }
    }
}
