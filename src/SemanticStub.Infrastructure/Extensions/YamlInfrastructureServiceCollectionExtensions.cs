using Microsoft.Extensions.DependencyInjection;
using SemanticStub.Application.Infrastructure.Yaml;
using SemanticStub.Infrastructure.Yaml;

namespace SemanticStub.Infrastructure.Extensions;

/// <summary>
/// Registers infrastructure services for YAML definition loading and reload monitoring.
/// </summary>
public static class YamlInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds YAML infrastructure services required by the stub runtime.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddYamlInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IStubDefinitionLoader, StubDefinitionLoader>();
        // The loaded YAML definition is process-wide runtime state and is replaced atomically on reload.
        services.AddSingleton<StubDefinitionState>();
        services.AddSingleton<IStubDefinitionVersionProvider>(serviceProvider => serviceProvider.GetRequiredService<StubDefinitionState>());
        services.AddHostedService<StubDefinitionStartupValidator>();
        services.AddHostedService<StubDefinitionWatcher>();

        return services;
    }
}
