using SemanticStub.Api.Models;

namespace SemanticStub.Api.Infrastructure.Yaml;

internal sealed class StubDefinitionValidator
{
    private const string JsonContentType = "application/json";

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

            ValidateOperation(pathEntry.Key, "get", pathEntry.Value.Get, definitionDirectory, errors);
            ValidateOperation(pathEntry.Key, "post", pathEntry.Value.Post, definitionDirectory, errors);
            ValidateOperation(pathEntry.Key, "put", pathEntry.Value.Put, definitionDirectory, errors);
            ValidateOperation(pathEntry.Key, "patch", pathEntry.Value.Patch, definitionDirectory, errors);
            ValidateOperation(pathEntry.Key, "delete", pathEntry.Value.Delete, definitionDirectory, errors);
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

        foreach (var responseEntry in operation.Responses)
        {
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

            if (match.Response.StatusCode <= 0)
            {
                errors.Add($"Path '{path}' {method.ToUpperInvariant()} x-match[{index}] must define a positive statusCode.");
            }

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
                errors.Add($"Path '{path}' {method.ToUpperInvariant()} {location} must define '{JsonContentType}' content or 'x-response-file'.");
            }

            return;
        }

        if (!content.TryGetValue(JsonContentType, out var mediaType))
        {
            errors.Add($"Path '{path}' {method.ToUpperInvariant()} {location} must define '{JsonContentType}' content or 'x-response-file'.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(responseFile))
        {
            // File-backed responses reuse the declared media type, so an inline example is optional.
            return;
        }

        if (mediaType.Example is null)
        {
            errors.Add($"Path '{path}' {method.ToUpperInvariant()} {location} must define an example for '{JsonContentType}'.");
        }
    }
}
