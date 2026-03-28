using SemanticStub.Api.Models;

namespace SemanticStub.Api.Infrastructure.Yaml;

internal sealed class StubDefinitionState
{
    private readonly IStubDefinitionLoader loader;
    private readonly ILogger<StubDefinitionState> logger;
    private readonly object syncRoot = new();
    private StubDocument currentDocument;

    public StubDefinitionState(IStubDefinitionLoader loader, ILogger<StubDefinitionState> logger)
    {
        this.loader = loader;
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
                Volatile.Write(ref currentDocument, reloadedDocument);
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
}
