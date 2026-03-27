using Microsoft.Extensions.Options;
using SemanticStub.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemanticStub.Api.Infrastructure.Yaml;

/// <summary>
/// Loads OpenAPI-based stub definitions from disk so the runtime can execute mock behavior directly from YAML without duplicating route metadata in code.
/// </summary>
public sealed class StubDefinitionLoader
{
    private const string DefaultStubFileName = "basic-routing.yaml";
    private static readonly string[] AdditionalStubFilePatterns = ["*.stub.yaml", "*.stub.yml"];
    private const string DefaultDefinitionsDirectoryName = "samples";
    private readonly IWebHostEnvironment environment;
    private readonly StubSettings settings;
    private readonly IDeserializer deserializer;
    private readonly StubDefinitionValidator validator;
    private readonly StubDefinitionNormalizer normalizer;

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
        validator = new StubDefinitionValidator();
        normalizer = new StubDefinitionNormalizer();
    }

    /// <summary>
    /// Discovers the default stub files, validates them, and merges them into one document so matching can run against a single deterministic view.
    /// </summary>
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

        validator.ValidateDocument(document, definitionDirectory);

        return normalizer.NormalizeDocument(document, definitionDirectory);
    }

    /// <summary>
    /// Resolves file-based response payloads relative to the definitions root so YAML response-file references behave consistently across environments.
    /// </summary>
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
            Parameters = MergeParameters(existing.Parameters, incoming.Parameters),
            Get = MergeOperation(path, "GET", existing.Get, incoming.Get, existingSource, incomingSource),
            Post = MergeOperation(path, "POST", existing.Post, incoming.Post, existingSource, incomingSource),
            Put = MergeOperation(path, "PUT", existing.Put, incoming.Put, existingSource, incomingSource),
            Patch = MergeOperation(path, "PATCH", existing.Patch, incoming.Patch, existingSource, incomingSource),
            Delete = MergeOperation(path, "DELETE", existing.Delete, incoming.Delete, existingSource, incomingSource)
        };
    }

    private static List<ParameterDefinition> MergeParameters(
        IReadOnlyCollection<ParameterDefinition> existing,
        IReadOnlyCollection<ParameterDefinition> incoming)
    {
        if (existing.Count == 0)
        {
            return [.. incoming];
        }

        if (incoming.Count == 0)
        {
            return [.. existing];
        }

        var merged = new List<ParameterDefinition>(existing.Count + incoming.Count);
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var parameter in existing)
        {
            if (keys.Add(GetParameterMergeKey(parameter)))
            {
                merged.Add(parameter);
            }
        }

        foreach (var parameter in incoming)
        {
            if (keys.Add(GetParameterMergeKey(parameter)))
            {
                merged.Add(parameter);
            }
        }

        return merged;
    }

    private static string GetParameterMergeKey(ParameterDefinition parameter)
    {
        var location = parameter.In.Trim();
        var name = location.Equals("header", StringComparison.OrdinalIgnoreCase)
            ? parameter.Name.Trim().ToUpperInvariant()
            : parameter.Name.Trim();

        return location.ToUpperInvariant() + ":" + name;
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

}
