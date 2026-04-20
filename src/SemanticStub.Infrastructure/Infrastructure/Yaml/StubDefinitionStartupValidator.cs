using Microsoft.Extensions.Hosting;

namespace SemanticStub.Infrastructure.Yaml;

internal sealed class StubDefinitionStartupValidator : IHostedService
{
    private readonly StubDefinitionState _state;

    public StubDefinitionStartupValidator(StubDefinitionState state)
    {
        _state = state;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _state.GetCurrentDocument();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
