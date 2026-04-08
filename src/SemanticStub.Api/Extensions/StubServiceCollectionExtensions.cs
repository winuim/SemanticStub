using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        services.AddHttpClient("SemanticEmbedding", (serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<StubSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(settings.SemanticMatching.TimeoutSeconds);
        });
        services.AddSingleton<SemanticEmbeddingClient>();
        services.AddSingleton<ISemanticMatcherService>(serviceProvider => new SemanticMatcherService(
            serviceProvider.GetRequiredService<SemanticEmbeddingClient>(),
            serviceProvider.GetRequiredService<IOptions<StubSettings>>(),
            serviceProvider.GetRequiredService<ILogger<SemanticMatcherService>>()));
        services.AddSingleton<IStubDefinitionLoader, StubDefinitionLoader>();
        services.AddSingleton<StubDefinitionState>();
        services.AddSingleton<StubInspectionRuntimeStore>();
        services.AddSingleton<StubInspectionScenarioCoordinator>();
        services.AddSingleton<IStubInspectionService>(serviceProvider => new StubInspectionService(
            serviceProvider.GetRequiredService<StubDefinitionState>(),
            serviceProvider.GetRequiredService<IStubDefinitionLoader>(),
            serviceProvider.GetRequiredService<IOptions<StubSettings>>(),
            serviceProvider.GetRequiredService<IStubService>(),
            serviceProvider.GetRequiredService<StubInspectionRuntimeStore>(),
            serviceProvider.GetRequiredService<StubInspectionScenarioCoordinator>()));
        services.AddHostedService<StubDefinitionWatcher>();
        services.AddSingleton(serviceProvider => new JsonBodyMatcher(
            serviceProvider.GetRequiredService<ILogger<JsonBodyMatcher>>()));
        services.AddSingleton<QueryValueMatcher>();
        services.AddSingleton(serviceProvider => new RegexQueryMatcher(
            serviceProvider.GetRequiredService<ILogger<RegexQueryMatcher>>()));
        services.AddSingleton<IMatcherService>(serviceProvider => new MatcherService(
            serviceProvider.GetRequiredService<JsonBodyMatcher>(),
            serviceProvider.GetRequiredService<QueryValueMatcher>(),
            serviceProvider.GetRequiredService<RegexQueryMatcher>()));
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
