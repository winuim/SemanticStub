using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

internal sealed class StubInspectionService : IStubInspectionService
{
    private readonly StubDefinitionState state;
    private readonly IStubDefinitionLoader loader;
    private readonly IOptions<StubSettings> settings;

    public StubInspectionService(
        StubDefinitionState state,
        IStubDefinitionLoader loader,
        IOptions<StubSettings> settings)
    {
        this.state = state;
        this.loader = loader;
        this.settings = settings;
    }

    /// <inheritdoc/>
    public StubConfigSnapshot GetConfigSnapshot()
    {
        var document = state.GetCurrentDocument();
        var routes = BuildRoutes(document);

        return new StubConfigSnapshot
        {
            SnapshotTimestamp = DateTimeOffset.UtcNow,
            ConfigurationHash = ComputeDocumentHash(document),
            DefinitionsDirectoryPath = loader.GetDefinitionsDirectoryPath(),
            RouteCount = routes.Count,
            SemanticMatchingEnabled = settings.Value.SemanticMatching.Enabled,
        };
    }

    /// <inheritdoc/>
    public IReadOnlyList<StubRouteInfo> GetRoutes()
    {
        var document = state.GetCurrentDocument();
        return BuildRoutes(document);
    }

    private static IReadOnlyList<StubRouteInfo> BuildRoutes(StubDocument document)
    {
        var routes = new List<StubRouteInfo>();

        foreach (var (path, pathItem) in document.Paths)
        {
            var operations = new (string Method, OperationDefinition? Op)[]
            {
                ("GET", pathItem.Get),
                ("POST", pathItem.Post),
                ("PUT", pathItem.Put),
                ("PATCH", pathItem.Patch),
                ("DELETE", pathItem.Delete),
            };

            foreach (var (method, op) in operations)
            {
                if (op is null) continue;

                routes.Add(new StubRouteInfo
                {
                    RouteId = string.IsNullOrEmpty(op.OperationId)
                        ? $"{method}:{path}"
                        : op.OperationId,
                    Method = method,
                    PathPattern = path,
                    UsesSemanticMatching = HasSemanticMatch(op),
                    UsesScenario = HasScenario(op),
                    ResponseCount = op.Responses.Count,
                });
            }
        }

        return routes;
    }

    private static bool HasSemanticMatch(OperationDefinition op)
        => op.Matches.Any(m => m.SemanticMatch is not null);

    private static bool HasScenario(OperationDefinition op)
        => op.Responses.Values.Any(r => r.Scenario is not null)
        || op.Matches.Any(m => m.Response.Scenario is not null);

    private static string ComputeDocumentHash(StubDocument document)
    {
        // Serialize a stable, ordered summary to avoid issues with object?-typed fields
        // and to ensure the hash changes whenever paths or methods are added/removed.
        var summary = document.Paths
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new
            {
                Path = p.Key,
                Methods = GetDefinedMethods(p.Value),
            });

        var json = JsonSerializer.Serialize(summary);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static IEnumerable<string> GetDefinedMethods(PathItemDefinition pathItem)
    {
        if (pathItem.Get is not null) yield return "GET";
        if (pathItem.Post is not null) yield return "POST";
        if (pathItem.Put is not null) yield return "PUT";
        if (pathItem.Patch is not null) yield return "PATCH";
        if (pathItem.Delete is not null) yield return "DELETE";
    }
}
