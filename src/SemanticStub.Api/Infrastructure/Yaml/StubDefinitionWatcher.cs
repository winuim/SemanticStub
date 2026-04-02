namespace SemanticStub.Api.Infrastructure.Yaml;

internal sealed class StubDefinitionWatcher : IHostedService, IDisposable
{
    private static readonly TimeSpan ReloadDebounceDelay = TimeSpan.FromMilliseconds(250);
    private readonly IStubDefinitionLoader loader;
    private readonly StubDefinitionState state;
    private readonly ILogger<StubDefinitionWatcher> logger;
    private readonly object syncRoot = new();
    private FileSystemWatcher? watcher;
    private Timer? reloadTimer;
    private string? pendingPath;

    public StubDefinitionWatcher(
        IStubDefinitionLoader loader,
        StubDefinitionState state,
        ILogger<StubDefinitionWatcher> logger)
    {
        this.loader = loader;
        this.state = state;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var definitionsPath = loader.GetDefinitionsDirectoryPath();
        watcher = new FileSystemWatcher(definitionsPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.DirectoryName
        };

        watcher.Changed += OnFileChanged;
        watcher.Created += OnFileChanged;
        watcher.Deleted += OnFileChanged;
        watcher.Renamed += OnFileRenamed;
        watcher.EnableRaisingEvents = true;

        logger.LogInformation("Watching stub definitions under '{DefinitionsPath}'.", definitionsPath);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (watcher is not null)
        {
            watcher.EnableRaisingEvents = false;
        }

        lock (syncRoot)
        {
            reloadTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (watcher is not null)
        {
            watcher.Changed -= OnFileChanged;
            watcher.Created -= OnFileChanged;
            watcher.Deleted -= OnFileChanged;
            watcher.Renamed -= OnFileRenamed;
            watcher.Dispose();
        }

        lock (syncRoot)
        {
            reloadTimer?.Dispose();
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs eventArgs)
    {
        if (!IsRelevantDefinitionPath(eventArgs.FullPath))
        {
            return;
        }

        ScheduleReload(eventArgs.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs eventArgs)
    {
        if (!IsRelevantDefinitionPath(eventArgs.FullPath) &&
            !IsRelevantDefinitionPath(eventArgs.OldFullPath))
        {
            return;
        }

        ScheduleReload(eventArgs.FullPath);
    }

    private void ScheduleReload(string path)
    {
        lock (syncRoot)
        {
            pendingPath = path;
            reloadTimer ??= new Timer(_ => ReloadDefinitions(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            reloadTimer.Change(ReloadDebounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void ReloadDefinitions()
    {
        string? changedPath;

        lock (syncRoot)
        {
            changedPath = pendingPath;
            pendingPath = null;
        }

        logger.LogInformation("Detected stub definition change at '{ChangedPath}'. Reloading definitions.", changedPath);
        state.TryReload();
    }

    private static bool IsRelevantDefinitionPath(string path)
    {
        var fileName = Path.GetFileName(path);

        if (string.Equals(fileName, "basic-routing.yaml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return fileName.EndsWith(".stub.yaml", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".stub.yml", StringComparison.OrdinalIgnoreCase);
    }
}
