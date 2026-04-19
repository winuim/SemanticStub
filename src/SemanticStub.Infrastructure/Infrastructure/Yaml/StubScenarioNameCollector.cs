using SemanticStub.Application.Models;

namespace SemanticStub.Infrastructure.Yaml;

internal static class StubScenarioNameCollector
{
    public static IReadOnlyList<string> Collect(StubDocument document)
    {
        var discoveredNames = new HashSet<string>(StringComparer.Ordinal);
        var scenarioNames = new List<string>();

        foreach (var pathItem in document.Paths.Values)
        {
            AddScenarioNames(pathItem.Get, discoveredNames, scenarioNames);
            AddScenarioNames(pathItem.Post, discoveredNames, scenarioNames);
            AddScenarioNames(pathItem.Put, discoveredNames, scenarioNames);
            AddScenarioNames(pathItem.Patch, discoveredNames, scenarioNames);
            AddScenarioNames(pathItem.Delete, discoveredNames, scenarioNames);
        }

        return scenarioNames;
    }

    private static void AddScenarioNames(
        OperationDefinition? operation,
        ISet<string> discoveredNames,
        ICollection<string> scenarioNames)
    {
        if (operation is null)
        {
            return;
        }

        foreach (var response in operation.Responses.Values)
        {
            AddScenarioName(response.Scenario, discoveredNames, scenarioNames);
        }

        foreach (var match in operation.Matches)
        {
            AddScenarioName(match.Response.Scenario, discoveredNames, scenarioNames);
        }
    }

    private static void AddScenarioName(
        ScenarioDefinition? scenario,
        ISet<string> discoveredNames,
        ICollection<string> scenarioNames)
    {
        if (scenario is null || !discoveredNames.Add(scenario.Name))
        {
            return;
        }

        scenarioNames.Add(scenario.Name);
    }
}
