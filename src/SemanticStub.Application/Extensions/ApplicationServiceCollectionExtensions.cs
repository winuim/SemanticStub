using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SemanticStub.Application.Services;

namespace SemanticStub.Application.Extensions;

/// <summary>
/// Registers application-layer services for matching and scenario state management.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the application services required by the stub runtime.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(serviceProvider => new JsonBodyMatcher(
            serviceProvider.GetRequiredService<ILogger<JsonBodyMatcher>>()));
        services.AddSingleton(serviceProvider => new FormBodyMatcher(
            serviceProvider.GetRequiredService<ILogger<FormBodyMatcher>>()));
        services.AddSingleton<QueryValueMatcher>();
        services.AddSingleton(serviceProvider => new RegexQueryMatcher(
            serviceProvider.GetRequiredService<ILogger<RegexQueryMatcher>>()));
        services.AddSingleton<MatcherService>(serviceProvider => new MatcherService(
            serviceProvider.GetRequiredService<JsonBodyMatcher>(),
            serviceProvider.GetRequiredService<FormBodyMatcher>(),
            serviceProvider.GetRequiredService<QueryValueMatcher>(),
            serviceProvider.GetRequiredService<RegexQueryMatcher>()));

        // YAML scenario progress is shared across requests for the current process.
        services.AddSingleton<ScenarioService>();

        return services;
    }
}
