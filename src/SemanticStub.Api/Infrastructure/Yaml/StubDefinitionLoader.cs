using Microsoft.Extensions.Options;
using SemanticStub.Api.Models;
using SemanticStub.Api.Utilities;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemanticStub.Api.Infrastructure.Yaml;

public sealed class StubDefinitionLoader
{
    private const string DefaultStubFileName = "basic-routing.yaml";
    private static readonly string[] AdditionalStubFilePatterns = ["*.stub.yaml", "*.stub.yml"];
    private const string DefaultDefinitionsDirectoryName = "samples";
    private const string JsonContentType = "application/json";
    private readonly IWebHostEnvironment environment;
    private readonly StubSettings settings;
    private readonly IDeserializer deserializer;

    public StubDefinitionLoader(IWebHostEnvironment environment)
        : this(environment, Options.Create(new StubSettings()))
    {
    }

    public StubDefinitionLoader(IWebHostEnvironment environment, IOptions<StubSettings> settings)
    {
        this.environment = environment;
        this.settings = settings.Value;
        deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public StubDocument LoadDefaultDefinition()
    {
        var definitionsRootPath = ResolveDefinitionsDirectory();
        var definitionPaths = ResolveDefinitionPaths();
        var documents = definitionPaths
            .Select(path => (Path: path, Label: GetDefinitionSourceLabel(path, definitionsRootPath), Document: LoadDefinition(path)))
            .ToArray();

        return MergeDefinitions(documents);
    }

    private StubDocument LoadDefinition(string path)
    {
        var yaml = File.ReadAllText(path);
        var document = deserializer.Deserialize<StubDocument>(yaml);
        var definitionDirectory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Could not determine definition directory for '{path}'.");

        if (document is null)
        {
            throw new InvalidOperationException("Failed to deserialize stub definition.");
        }

        ValidateDocument(document, definitionDirectory);

        return NormalizeDocument(document, definitionDirectory);
    }

    public string LoadResponseFileContent(string fileName)
    {
        if (Path.IsPathRooted(fileName))
        {
            return File.ReadAllText(fileName);
        }

        var path = ResolveSamplePath(fileName);

        return File.ReadAllText(path);
    }

    private string[] ResolveDefinitionPaths()
    {
        var samplesPath = ResolveDefinitionsDirectory();
        var paths = new List<string>();
        var defaultDefinitionPath = Path.Combine(samplesPath, DefaultStubFileName);

        if (File.Exists(defaultDefinitionPath))
        {
            paths.Add(defaultDefinitionPath);
        }

        foreach (var pattern in AdditionalStubFilePatterns)
        {
            var discoveredPaths = Directory
                .GetFiles(samplesPath, pattern, SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal);

            foreach (var discoveredPath in discoveredPaths)
            {
                if (!paths.Contains(discoveredPath, StringComparer.Ordinal))
                {
                    paths.Add(discoveredPath);
                }
            }
        }

        if (paths.Count > 0)
        {
            return [.. paths];
        }

        throw new FileNotFoundException(
            $"Could not locate {Path.Combine(GetDefinitionsPathLabel(), DefaultStubFileName)} from the current content root.",
            Path.Combine(GetDefinitionsPathLabel(), DefaultStubFileName));
    }

    private string ResolveSamplePath(string fileName)
    {
        var samplesPath = ResolveDefinitionsDirectory();
        var candidate = Path.Combine(samplesPath, fileName);

        if (File.Exists(candidate))
        {
            return candidate;
        }

        throw new FileNotFoundException(
            $"Could not locate {Path.Combine(GetDefinitionsPathLabel(), fileName)} from the current content root.",
            Path.Combine(GetDefinitionsPathLabel(), fileName));
    }

    private string ResolveDefinitionsDirectory()
    {
        if (!string.IsNullOrWhiteSpace(settings.DefinitionsPath))
        {
            var configuredPath = settings.DefinitionsPath!;

            if (Path.IsPathRooted(configuredPath))
            {
                if (Directory.Exists(configuredPath))
                {
                    return configuredPath;
                }

                throw new DirectoryNotFoundException($"Could not locate configured definitions path '{configuredPath}'.");
            }

            var configuredCurrent = new DirectoryInfo(environment.ContentRootPath);

            while (configuredCurrent is not null)
            {
                var candidate = Path.Combine(configuredCurrent.FullName, configuredPath);

                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                configuredCurrent = configuredCurrent.Parent;
            }

            throw new DirectoryNotFoundException($"Could not locate configured definitions path '{configuredPath}'.");
        }

        var current = new DirectoryInfo(environment.ContentRootPath);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, DefaultDefinitionsDirectoryName);

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not locate {DefaultDefinitionsDirectoryName} from the current content root.", DefaultDefinitionsDirectoryName);
    }

    private string GetDefinitionsPathLabel()
    {
        return string.IsNullOrWhiteSpace(settings.DefinitionsPath)
            ? DefaultDefinitionsDirectoryName
            : settings.DefinitionsPath!;
    }

    private static StubDocument MergeDefinitions(IReadOnlyCollection<(string Path, string Label, StubDocument Document)> sources)
    {
        if (sources.Count == 1)
        {
            return sources.First().Document;
        }

        var openApiVersions = sources
            .Select(source => source.Document.OpenApi)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (openApiVersions.Length != 1)
        {
            throw new InvalidOperationException("Stub definition files must use the same 'openapi' version.");
        }

        var mergedPaths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal);
        var pathSources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            foreach (var pathEntry in source.Document.Paths)
            {
                if (!mergedPaths.TryGetValue(pathEntry.Key, out var existingPathItem))
                {
                    mergedPaths[pathEntry.Key] = pathEntry.Value;
                    pathSources[pathEntry.Key] = source.Label;
                    continue;
                }

                var mergedPathItem = MergePathItem(
                    pathEntry.Key,
                    existingPathItem,
                    pathEntry.Value,
                    pathSources[pathEntry.Key],
                    source.Label);

                mergedPaths[pathEntry.Key] = mergedPathItem;
            }
        }

        return new StubDocument
        {
            OpenApi = openApiVersions[0],
            Paths = mergedPaths
        };
    }

    private static PathItemDefinition MergePathItem(
        string path,
        PathItemDefinition existing,
        PathItemDefinition incoming,
        string existingSource,
        string incomingSource)
    {
        return new PathItemDefinition
        {
            Get = MergeOperation(path, "GET", existing.Get, incoming.Get, existingSource, incomingSource),
            Post = MergeOperation(path, "POST", existing.Post, incoming.Post, existingSource, incomingSource)
        };
    }

    private static OperationDefinition? MergeOperation(
        string path,
        string method,
        OperationDefinition? existing,
        OperationDefinition? incoming,
        string existingSource,
        string incomingSource)
    {
        if (existing is null)
        {
            return incoming;
        }

        if (incoming is null)
        {
            return existing;
        }

        throw new InvalidOperationException(
            $"Path '{path}' {method} is defined in both '{existingSource}' and '{incomingSource}'.");
    }

    private static string GetDefinitionSourceLabel(string definitionPath, string definitionsRootPath)
    {
        return Path.GetRelativePath(definitionsRootPath, definitionPath);
    }

    private void ValidateDocument(StubDocument document, string definitionDirectory)
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
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Invalid stub definition:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(error => "- " + error)));
            }

            return;
        }

        foreach (var pathEntry in paths)
        {
            if (pathEntry.Value.Get is null && pathEntry.Value.Post is null)
            {
                errors.Add($"Path '{pathEntry.Key}' must define at least one supported operation.");
                continue;
            }

            ValidateOperation(pathEntry.Key, "get", pathEntry.Value.Get, definitionDirectory, errors);
            ValidateOperation(pathEntry.Key, "post", pathEntry.Value.Post, definitionDirectory, errors);
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Invalid stub definition:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(error => "- " + error)));
        }
    }

    private void ValidateOperation(
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

    private void ValidateResponseDefinition(
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
            var resolvedPath = ResolveResponseFilePath(definitionDirectory, responseFile);

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
            return;
        }

        if (mediaType.Example is null)
        {
            errors.Add($"Path '{path}' {method.ToUpperInvariant()} {location} must define an example for '{JsonContentType}'.");
        }
    }

    private static StubDocument NormalizeDocument(StubDocument document, string definitionDirectory)
    {
        return new StubDocument
        {
            OpenApi = document.OpenApi,
            Paths = document.Paths.ToDictionary(
                entry => entry.Key,
                entry => NormalizePathItem(entry.Value, definitionDirectory),
                StringComparer.Ordinal)
        };
    }

    private static PathItemDefinition NormalizePathItem(PathItemDefinition pathItem, string definitionDirectory)
    {
        return new PathItemDefinition
        {
            Get = NormalizeOperation(pathItem.Get, definitionDirectory),
            Post = NormalizeOperation(pathItem.Post, definitionDirectory)
        };
    }

    private static OperationDefinition? NormalizeOperation(OperationDefinition? operation, string definitionDirectory)
    {
        if (operation is null)
        {
            return null;
        }

        return new OperationDefinition
        {
            OperationId = operation.OperationId,
            Matches =
            [
                .. operation.Matches.Select(match => new QueryMatchDefinition
                {
                    Query = new Dictionary<string, string>(match.Query, StringComparer.Ordinal),
                    Body = StubExampleSerializer.NormalizeValue(match.Body),
                    Response = new QueryMatchResponseDefinition
                    {
                        StatusCode = match.Response.StatusCode,
                        ResponseFile = ResolveResponseFilePath(definitionDirectory, match.Response.ResponseFile),
                        Content = new Dictionary<string, MediaTypeDefinition>(match.Response.Content, StringComparer.Ordinal)
                    }
                })
            ],
            Responses = operation.Responses.ToDictionary(
                entry => entry.Key,
                entry => new ResponseDefinition
                {
                    Description = entry.Value.Description,
                    ResponseFile = ResolveResponseFilePath(definitionDirectory, entry.Value.ResponseFile),
                    Content = new Dictionary<string, MediaTypeDefinition>(entry.Value.Content, StringComparer.Ordinal)
                },
                StringComparer.Ordinal)
        };
    }

    private static string? ResolveResponseFilePath(string definitionDirectory, string? responseFile)
    {
        if (string.IsNullOrWhiteSpace(responseFile))
        {
            return responseFile;
        }

        if (Path.IsPathRooted(responseFile))
        {
            return responseFile;
        }

        return Path.GetFullPath(Path.Combine(definitionDirectory, responseFile));
    }

}
