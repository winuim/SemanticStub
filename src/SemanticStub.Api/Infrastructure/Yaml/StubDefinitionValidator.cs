using SemanticStub.Api.Models;

namespace SemanticStub.Api.Infrastructure.Yaml;

internal sealed class StubDefinitionValidator
{

    public void ValidateDocument(StubDocument document, string definitionDirectory)
    {
        var errors = new List<string>();
        var paths = document.Paths;

        if (string.IsNullOrWhiteSpace(document.OpenApi))
        {
            errors.Add("The 'openapi' field is required.");
        }

        if (paths is null || paths.Count == 0)
        {
            errors.Add("At least one path must be configured under 'paths'.");
        }

        if (paths is null)
        {
            ThrowIfInvalid(errors);
            return;
        }

        foreach (var pathEntry in paths)
        {
            if (pathEntry.Value.Get is null &&
                pathEntry.Value.Post is null &&
                pathEntry.Value.Put is null &&
                pathEntry.Value.Patch is null &&
                pathEntry.Value.Delete is null)
            {
                errors.Add($"Path '{pathEntry.Key}' must define at least one supported operation.");
                continue;
            }

            ValidateOperation(pathEntry.Key, "get", pathEntry.Value.Parameters, pathEntry.Value.Get, definitionDirectory, errors);
            ValidateOperation(pathEntry.Key, "post", pathEntry.Value.Parameters, pathEntry.Value.Post, definitionDirectory, errors);
            ValidateOperation(pathEntry.Key, "put", pathEntry.Value.Parameters, pathEntry.Value.Put, definitionDirectory, errors);
            ValidateOperation(pathEntry.Key, "patch", pathEntry.Value.Parameters, pathEntry.Value.Patch, definitionDirectory, errors);
            ValidateOperation(pathEntry.Key, "delete", pathEntry.Value.Parameters, pathEntry.Value.Delete, definitionDirectory, errors);
        }

        ThrowIfInvalid(errors);
    }

    private static void ThrowIfInvalid(IReadOnlyCollection<string> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Invalid stub definition:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(error => "- " + error)));
    }

    private static void ValidateOperation(
        string path,
        string method,
        IReadOnlyCollection<ParameterDefinition> pathParameters,
        OperationDefinition? operation,
        string definitionDirectory,
        ICollection<string> errors)
    {
        if (operation is null)
        {
            return;
        }

        if (operation.Responses.Count == 0 && operation.Matches.Count == 0)
        {
            errors.Add($"Path '{path}' {method.ToUpperInvariant()} must define at least one response or x-match entry.");
        }

        var queryParameters = GetDeclaredQueryParameters(pathParameters, operation.Parameters);
        var headerParameters = GetDeclaredHeaderParameters(pathParameters, operation.Parameters);

        foreach (var responseEntry in operation.Responses)
        {
            ValidateDelayDefinition(
                path,
                method,
                $"responses['{responseEntry.Key}']",
                responseEntry.Value.DelayMilliseconds,
                errors);

            ValidateResponseDefinition(
                path,
                method,
                $"responses['{responseEntry.Key}']",
                responseEntry.Key,
                responseEntry.Value.ResponseFile,
                definitionDirectory,
                responseEntry.Value.Content,
                errors);
        }

        for (var index = 0; index < operation.Matches.Count; index++)
        {
            var match = operation.Matches[index];

            foreach (var queryKey in match.Query.Keys)
            {
                if (queryParameters.Count > 0 && !queryParameters.Contains(queryKey))
                {
                    errors.Add(
                        $"Path '{path}' {method.ToUpperInvariant()} x-match[{index}].query['{queryKey}'] must reference a declared query parameter.");
                }
            }

            foreach (var queryKey in match.PartialQuery.Keys)
            {
                if (queryParameters.Count > 0 && !queryParameters.Contains(queryKey))
                {
                    errors.Add(
                        $"Path '{path}' {method.ToUpperInvariant()} x-match[{index}].x-query-partial['{queryKey}'] must reference a declared query parameter.");
                }
            }

            foreach (var headerKey in match.Headers.Keys)
            {
                if (headerParameters.Count > 0 && !headerParameters.Contains(headerKey))
                {
                    errors.Add(
                        $"Path '{path}' {method.ToUpperInvariant()} x-match[{index}].headers['{headerKey}'] must reference a declared header parameter.");
                }
            }

            if (match.Response.StatusCode <= 0)
            {
                errors.Add($"Path '{path}' {method.ToUpperInvariant()} x-match[{index}] must define a positive statusCode.");
            }

            ValidateDelayDefinition(
                path,
                method,
                $"x-match[{index}].response",
                match.Response.DelayMilliseconds,
                errors);

            ValidateResponseDefinition(
                path,
                method,
                $"x-match[{index}].response",
                match.Response.StatusCode.ToString(),
                match.Response.ResponseFile,
                definitionDirectory,
                match.Response.Content,
                errors);
        }
    }

    private static HashSet<string> GetDeclaredQueryParameters(
        IReadOnlyCollection<ParameterDefinition> pathParameters,
        IReadOnlyCollection<ParameterDefinition> operationParameters)
    {
        return GetDeclaredParameters(pathParameters, operationParameters, "query", StringComparer.Ordinal);
    }

    private static HashSet<string> GetDeclaredHeaderParameters(
        IReadOnlyCollection<ParameterDefinition> pathParameters,
        IReadOnlyCollection<ParameterDefinition> operationParameters)
    {
        return GetDeclaredParameters(pathParameters, operationParameters, "header", StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> GetDeclaredParameters(
        IReadOnlyCollection<ParameterDefinition> pathParameters,
        IReadOnlyCollection<ParameterDefinition> operationParameters,
        string parameterLocation,
        StringComparer comparer)
    {
        var declaredParameters = new HashSet<string>(comparer);

        AddDeclaredParameters(pathParameters, parameterLocation, declaredParameters);
        AddDeclaredParameters(operationParameters, parameterLocation, declaredParameters);

        return declaredParameters;
    }

    private static void AddDeclaredParameters(
        IReadOnlyCollection<ParameterDefinition> parameters,
        string parameterLocation,
        ISet<string> declaredParameters)
    {
        foreach (var parameter in parameters)
        {
            if (string.Equals(parameter.In, parameterLocation, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(parameter.Name))
            {
                declaredParameters.Add(parameter.Name);
            }
        }
    }

    private static void ValidateResponseDefinition(
        string path,
        string method,
        string location,
        string statusCode,
        string? responseFile,
        string definitionDirectory,
        IReadOnlyDictionary<string, MediaTypeDefinition> content,
        ICollection<string> errors)
    {
        if (!int.TryParse(statusCode, out _))
        {
            errors.Add($"Path '{path}' {method.ToUpperInvariant()} {location} uses unsupported response key '{statusCode}'.");
        }

        if (!string.IsNullOrWhiteSpace(responseFile))
        {
            var resolvedPath = StubDefinitionPathResolver.ResolveResponseFilePath(definitionDirectory, responseFile);

            if (!File.Exists(resolvedPath))
            {
                errors.Add($"Path '{path}' {method.ToUpperInvariant()} {location} references missing response file '{responseFile}'.");
            }
        }

        if (content.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(responseFile))
            {
                errors.Add($"Path '{path}' {method.ToUpperInvariant()} {location} must define content or 'x-response-file'.");
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(responseFile))
        {
            // File-backed responses reuse the declared media type, so an inline example is optional.
            return;
        }

        // At least one media type must provide an inline example.
        // We report against the first entry to give a concrete target in the error message.
        if (content.All(static entry => entry.Value.Example is null))
        {
            var firstKey = content.First().Key;
            errors.Add($"Path '{path}' {method.ToUpperInvariant()} {location} must define an example for '{firstKey}'.");
        }
    }

    private static void ValidateDelayDefinition(
        string path,
        string method,
        string location,
        int? delayMilliseconds,
        ICollection<string> errors)
    {
        if (delayMilliseconds is < 0)
        {
            errors.Add($"Path '{path}' {method.ToUpperInvariant()} {location} must define a non-negative x-delay.");
        }
    }
}
