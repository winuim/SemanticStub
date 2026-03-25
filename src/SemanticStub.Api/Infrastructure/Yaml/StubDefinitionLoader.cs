using System.Text.Json;
using SemanticStub.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemanticStub.Api.Infrastructure.Yaml;

public sealed class StubDefinitionLoader
{
    private const string DefaultStubFileName = "basic-routing.yaml";
    private const string JsonContentType = "application/json";
    private readonly IWebHostEnvironment environment;
    private readonly IDeserializer deserializer;

    public StubDefinitionLoader(IWebHostEnvironment environment)
    {
        this.environment = environment;
        deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public StubDocument LoadDefaultDefinition()
    {
        var path = ResolveSamplePath(DefaultStubFileName);
        return LoadDefinition(path);
    }

    public StubDocument LoadDefinition(string path)
    {
        var yaml = File.ReadAllText(path);
        var document = deserializer.Deserialize<StubDocument>(yaml);

        if (document is null)
        {
            throw new InvalidOperationException("Failed to deserialize stub definition.");
        }

        ValidateDocument(document);

        return document;
    }

    public string LoadResponseFileContent(string fileName)
    {
        var path = ResolveSamplePath(fileName);

        return File.ReadAllText(path);
    }

    private string ResolveSamplePath(string fileName)
    {
        var current = new DirectoryInfo(environment.ContentRootPath);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "samples", fileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not locate samples/{fileName} from the current content root.", Path.Combine("samples", fileName));
    }

    private void ValidateDocument(StubDocument document)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(document.OpenApi))
        {
            errors.Add("The 'openapi' field is required.");
        }

        if (document.Paths.Count == 0)
        {
            errors.Add("At least one path must be configured under 'paths'.");
        }

        foreach (var pathEntry in document.Paths)
        {
            if (pathEntry.Value.Get is null && pathEntry.Value.Post is null)
            {
                errors.Add($"Path '{pathEntry.Key}' must define at least one supported operation.");
                continue;
            }

            ValidateOperation(pathEntry.Key, "get", pathEntry.Value.Get, errors);
            ValidateOperation(pathEntry.Key, "post", pathEntry.Value.Post, errors);
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Invalid stub definition:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(error => "- " + error)));
        }
    }

    private void ValidateOperation(string path, string method, OperationDefinition? operation, ICollection<string> errors)
    {
        if (operation is null)
        {
            return;
        }

        foreach (var responseEntry in operation.Responses)
        {
            ValidateResponseDefinition(
                path,
                method,
                $"responses['{responseEntry.Key}']",
                responseEntry.Key,
                responseEntry.Value.ResponseFile,
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
                match.Response.Content,
                errors);
        }
    }

    private void ValidateResponseDefinition(
        string path,
        string method,
        string location,
        string statusCode,
        string? responseFile,
        IReadOnlyDictionary<string, MediaTypeDefinition> content,
        ICollection<string> errors)
    {
        if (!int.TryParse(statusCode, out _))
        {
            errors.Add($"Path '{path}' {method.ToUpperInvariant()} {location} uses unsupported response key '{statusCode}'.");
        }

        if (!string.IsNullOrWhiteSpace(responseFile))
        {
            try
            {
                ResolveSamplePath(responseFile);
            }
            catch (FileNotFoundException)
            {
                errors.Add($"Path '{path}' {method.ToUpperInvariant()} {location} references missing response file '{responseFile}'.");
            }

            return;
        }

        if (!content.TryGetValue(JsonContentType, out var mediaType))
        {
            errors.Add($"Path '{path}' {method.ToUpperInvariant()} {location} must define '{JsonContentType}' content or 'x-response-file'.");
            return;
        }

        if (mediaType.Example is null)
        {
            errors.Add($"Path '{path}' {method.ToUpperInvariant()} {location} must define an example for '{JsonContentType}'.");
        }
    }

    public static string SerializeExample(object? example)
    {
        var normalized = NormalizeYamlValue(example);

        return JsonSerializer.Serialize(normalized);
    }

    private static object? NormalizeYamlValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is IDictionary<object, object> dictionary)
        {
            var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var entry in dictionary)
            {
                normalized[entry.Key.ToString() ?? string.Empty] = NormalizeYamlValue(entry.Value);
            }

            return normalized;
        }

        if (value is IEnumerable<object> list && value is not string)
        {
            return list.Select(NormalizeYamlValue).ToList();
        }

        return value;
    }
}
