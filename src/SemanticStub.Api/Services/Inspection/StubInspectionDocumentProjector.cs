using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

internal static class StubInspectionDocumentProjector
{
    public static IReadOnlyList<StubRouteInfo> BuildRoutes(StubDocument document)
    {
        var routes = new List<StubRouteInfo>();

        foreach (var (path, pathItem) in document.Paths)
        {
            var operations = EnumerateOperations(pathItem);

            foreach (var (method, op) in operations)
            {
                if (op is null)
                {
                    continue;
                }

                routes.Add(new StubRouteInfo
                {
                    RouteId = GetRouteId(method, path, op),
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

    public static StubRouteDetailInfo? FindRoute(StubDocument document, string routeId)
    {
        foreach (var (path, pathItem) in document.Paths)
        {
            foreach (var (method, op) in EnumerateOperations(pathItem))
            {
                if (op is null)
                {
                    continue;
                }

                if (!string.Equals(GetRouteId(method, path, op), routeId, StringComparison.Ordinal))
                {
                    continue;
                }

                return new StubRouteDetailInfo
                {
                    RouteId = routeId,
                    Method = method,
                    PathPattern = path,
                    UsesSemanticMatching = HasSemanticMatch(op),
                    UsesScenario = HasScenario(op),
                    ResponseCount = op.Responses.Count,
                    HasConditionalMatches = op.Matches.Count > 0,
                    Responses = BuildResponses(op),
                    ConditionalMatches = BuildConditionalMatches(op),
                };
            }
        }

        return null;
    }

    public static IReadOnlyList<string> GetScenarioNames(StubDocument document)
    {
        var scenarioNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pathItem in document.Paths.Values)
        {
            AddScenarioNames(pathItem.Get, scenarioNames);
            AddScenarioNames(pathItem.Post, scenarioNames);
            AddScenarioNames(pathItem.Put, scenarioNames);
            AddScenarioNames(pathItem.Patch, scenarioNames);
            AddScenarioNames(pathItem.Delete, scenarioNames);
        }

        return scenarioNames.OrderBy(name => name, StringComparer.Ordinal).ToList();
    }

    public static string ComputeDocumentHash(StubDocument document)
    {
        // Serialize a stable, ordered summary of the full route configuration.
        // Includes operation-level details (operationId, response keys, match rules,
        // semantic match descriptions) so that changes within existing routes are reflected
        // in the hash, not just path/method presence changes.
        // Avoids object?-typed fields (query dicts, body) to prevent serialization issues.
        var summary = document.Paths
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new
            {
                Path = p.Key,
                Operations = GetOperationSummaries(p.Value),
            });

        var json = JsonSerializer.Serialize(summary);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static IReadOnlyList<StubRouteResponseInfo> BuildResponses(OperationDefinition operation)
    {
        return operation.Responses
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new StubRouteResponseInfo
            {
                ResponseId = entry.Key,
                UsesScenario = entry.Value.Scenario is not null,
                Scenario = BuildScenario(entry.Value.Scenario),
            })
            .ToList();
    }

    private static IReadOnlyList<StubRouteConditionInfo> BuildConditionalMatches(OperationDefinition operation)
    {
        return operation.Matches
            .Select((match, index) => new StubRouteConditionInfo
            {
                CandidateIndex = index,
                HasExactQuery = GetEqualsKeys(match.Query).Count > 0,
                ExactQueryKeys = GetEqualsKeys(match.Query),
                HasRegexQuery = GetRegexKeys(match.Query).Count > 0,
                RegexQueryKeys = GetRegexKeys(match.Query),
                HeaderKeys = OrderKeys(match.Headers.Keys),
                HasBody = match.Body is not null,
                UsesSemanticMatching = match.SemanticMatch is not null,
                ResponseStatusCode = match.Response.StatusCode,
                UsesScenario = match.Response.Scenario is not null,
                Scenario = BuildScenario(match.Response.Scenario),
            })
            .ToList();
    }

    private static StubRouteScenarioInfo? BuildScenario(ScenarioDefinition? scenario)
    {
        return scenario is null
            ? null
            : new StubRouteScenarioInfo
            {
                Name = scenario.Name,
                State = scenario.State,
                Next = scenario.Next,
            };
    }

    private static IReadOnlyList<string> OrderKeys(IEnumerable<string> keys)
        => keys.OrderBy(key => key, StringComparer.Ordinal).ToList();

    private static IReadOnlyList<string> GetEqualsKeys(IReadOnlyDictionary<string, object?> fields)
        => fields
            .Where(field => MatchOperatorDefinition.TryGetEquals(field.Value, out _))
            .Select(field => field.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> GetRegexKeys(IReadOnlyDictionary<string, object?> fields)
        => fields
            .Where(field => MatchOperatorDefinition.TryGetRegex(field.Value, out _))
            .Select(field => field.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

    private static string GetRouteId(string method, string path, OperationDefinition operation)
        => string.IsNullOrEmpty(operation.OperationId)
            ? $"{method}:{path}"
            : operation.OperationId;

    private static (string Method, OperationDefinition? Op)[] EnumerateOperations(PathItemDefinition pathItem)
    {
        return
        [
            ("GET", pathItem.Get),
            ("POST", pathItem.Post),
            ("PUT", pathItem.Put),
            ("PATCH", pathItem.Patch),
            ("DELETE", pathItem.Delete),
        ];
    }

    private static bool HasSemanticMatch(OperationDefinition op)
        => op.Matches.Any(m => m.SemanticMatch is not null);

    private static bool HasScenario(OperationDefinition op)
        => op.Responses.Values.Any(r => r.Scenario is not null)
        || op.Matches.Any(m => m.Response.Scenario is not null);

    private static void AddScenarioNames(OperationDefinition? operation, ISet<string> scenarioNames)
    {
        if (operation is null)
        {
            return;
        }

        foreach (var response in operation.Responses.Values)
        {
            if (response.Scenario is not null)
            {
                scenarioNames.Add(response.Scenario.Name);
            }
        }

        foreach (var match in operation.Matches)
        {
            if (match.Response.Scenario is not null)
            {
                scenarioNames.Add(match.Response.Scenario.Name);
            }
        }
    }

    private static IEnumerable<object> GetOperationSummaries(PathItemDefinition pathItem)
    {
        foreach (var (method, op) in EnumerateOperations(pathItem))
        {
            if (op is null)
            {
                continue;
            }

            yield return new
            {
                Method = method,
                OperationId = op.OperationId,
                Responses = op.Responses.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                MatchCount = op.Matches.Count,
                SemanticMatches = op.Matches
                    .Where(m => m.SemanticMatch is not null)
                    .Select(m => m.SemanticMatch!)
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToList(),
            };
        }
    }
}
