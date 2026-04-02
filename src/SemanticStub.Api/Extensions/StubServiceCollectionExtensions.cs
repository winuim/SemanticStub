using Microsoft.Extensions.DependencyInjection;
using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Services;

namespace SemanticStub.Api.Extensions;

/// <summary>
/// Registers the stub runtime services used to load definitions, watch reloads, and resolve responses.
/// </summary>
public static class StubServiceCollectionExtensions
{
    /// <summary>
    /// Adds the SemanticStub runtime services required by the API host.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddStubServices(this IServiceCollection services)
    {
        services.AddHttpClient<ISemanticMatcherService, SemanticMatcherService>();
        services.AddSingleton<IStubDefinitionLoader, StubDefinitionLoader>();
        services.AddSingleton<StubDefinitionState>();
        services.AddHostedService<StubDefinitionWatcher>();
        services.AddSingleton<IMatcherService, MatcherService>();
        services.AddSingleton<ScenarioService>();
        services.AddSingleton<IStubService>(serviceProvider => new StubService(
            serviceProvider.GetRequiredService<StubDefinitionState>(),
            serviceProvider.GetRequiredService<IMatcherService>(),
            serviceProvider.GetRequiredService<ScenarioService>(),
            serviceProvider.GetRequiredService<ISemanticMatcherService>(),
            serviceProvider.GetRequiredService<ILogger<StubService>>()));

        return services;
    }
}
