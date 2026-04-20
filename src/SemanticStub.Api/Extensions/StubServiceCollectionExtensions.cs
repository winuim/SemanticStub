using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SemanticStub.Application.Extensions;
using SemanticStub.Application.Infrastructure.Yaml;
using SemanticStub.Application.Services;
using SemanticStub.Infrastructure.Extensions;
using SemanticStub.Infrastructure.Semantic;
using SemanticStub.Infrastructure.Yaml;
using SemanticStub.Application.Services.Semantic;
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
        // Keep resolution registration in this chain because inspection resolves IStubService lazily.
        return services
            .AddSemanticMatchingServices()
            .AddApplicationServices()
            .AddYamlInfrastructureServices()
            .AddInspectionServices()
            .AddResolutionServices();
    }

    private static IServiceCollection AddSemanticMatchingServices(this IServiceCollection services)
    {
        services.AddHttpClient("SemanticEmbedding", (serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<StubSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(settings.SemanticMatching.TimeoutSeconds);
        });
        services.AddSingleton<ISemanticEmbeddingClient, SemanticEmbeddingClient>();
        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<StubSettings>>().Value);
        services.AddSingleton<ISemanticMatcherService, SemanticMatcherService>();

        return services;
    }

    private static IServiceCollection AddInspectionServices(this IServiceCollection services)
    {
        // Runtime inspection metrics and recent request history are process-wide by design.
        services.AddSingleton<StubInspectionRuntimeStore>();
        services.AddSingleton<StubInspectionScenarioCoordinator>();
        services.AddSingleton<IStubInspectionService, StubInspectionService>();

        return services;
    }

    private static IServiceCollection AddResolutionServices(this IServiceCollection services)
    {
        services.AddSingleton<Func<string, string>>(serviceProvider =>
            serviceProvider.GetRequiredService<StubDefinitionState>().LoadResponseFileContent);
        services.AddSingleton<StubResponseBuilder>();
        services.AddSingleton<StubDefaultResponseSelector>();
        services.AddSingleton<StubDispatchSelector>();
        services.AddSingleton<StubInspectionProjectionBuilder>();
        services.AddSingleton<IStubService>(serviceProvider => new StubService(
            serviceProvider.GetRequiredService<StubDefinitionState>(),
            serviceProvider.GetRequiredService<MatcherService>(),
            serviceProvider.GetRequiredService<ScenarioService>(),
            serviceProvider.GetRequiredService<StubDispatchSelector>(),
            serviceProvider.GetRequiredService<StubInspectionProjectionBuilder>()));

        return services;
    }
}
