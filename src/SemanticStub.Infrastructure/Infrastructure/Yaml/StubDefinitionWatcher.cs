using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SemanticStub.Application.Infrastructure.Yaml;

namespace SemanticStub.Infrastructure.Yaml;

internal sealed class StubDefinitionWatcher : IHostedService, IDisposable
{
    private static readonly TimeSpan ReloadDebounceDelay = TimeSpan.FromMilliseconds(250);
    private readonly IStubDefinitionLoader _loader;
    private readonly StubDefinitionState _state;
    private readonly ILogger<StubDefinitionWatcher> _logger;
    private readonly object _syncRoot = new();
    private FileSystemWatcher? _watcher;
    private Timer? _reloadTimer;
    private string? _pendingPath;

    public StubDefinitionWatcher(
        IStubDefinitionLoader loader,
        StubDefinitionState state,
        ILogger<StubDefinitionWatcher> logger)
    {
        _loader = loader;
        _state = state;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var definitionsPath = _loader.GetDefinitionsDirectoryPath();
        _watcher = new FileSystemWatcher(definitionsPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.DirectoryName
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.EnableRaisingEvents = true;

        _logger.LogInformation("Watching stub definitions under '{DefinitionsPath}'.", definitionsPath);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
        }

        lock (_syncRoot)
        {
            _reloadTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_watcher is not null)
        {
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Deleted -= OnFileChanged;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Dispose();
        }

        lock (_syncRoot)
        {
            _reloadTimer?.Dispose();
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
        lock (_syncRoot)
        {
            _pendingPath = path;
            _reloadTimer ??= new Timer(_ => ReloadDefinitions(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _reloadTimer.Change(ReloadDebounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void ReloadDefinitions()
    {
        string? changedPath;

        lock (_syncRoot)
        {
            changedPath = _pendingPath;
            _pendingPath = null;
        }

        _logger.LogInformation("Detected stub definition change at '{ChangedPath}'. Reloading definitions.", changedPath);
        _state.TryReload();
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
