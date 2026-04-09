using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

internal static class QueryParameterTypeMapBuilder
{
    public static IReadOnlyDictionary<string, string> Build(
        IReadOnlyCollection<ParameterDefinition> pathParameters,
        IReadOnlyCollection<ParameterDefinition> operationParameters)
    {
        var queryParameterTypes = new Dictionary<string, string>(StringComparer.Ordinal);

        Add(pathParameters, queryParameterTypes);
        Add(operationParameters, queryParameterTypes);

        return queryParameterTypes;
    }

    private static void Add(
        IReadOnlyCollection<ParameterDefinition> parameters,
        IDictionary<string, string> queryParameterTypes)
    {
        foreach (var parameter in parameters)
        {
            if (!string.Equals(parameter.In, "query", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(parameter.Name) ||
                string.IsNullOrWhiteSpace(parameter.Schema?.Type))
            {
                continue;
            }

            queryParameterTypes[parameter.Name] = parameter.Schema.Type;
        }
    }
}
