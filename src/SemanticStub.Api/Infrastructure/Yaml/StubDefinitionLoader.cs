using Microsoft.Extensions.Options;
using SemanticStub.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemanticStub.Api.Infrastructure.Yaml;

/// <summary>
/// Loads OpenAPI-based stub definitions from disk, validates repository-specific constraints, and normalizes file-backed references before the runtime uses the document.
/// </summary>
public sealed class StubDefinitionLoader : IStubDefinitionLoader
{
    private const string DefaultStubFileName = "basic-routing.yaml";
    private static readonly string[] AdditionalStubFilePatterns = ["*.stub.yaml", "*.stub.yml"];
    private const string DefaultDefinitionsDirectoryName = "samples";
    private readonly IWebHostEnvironment environment;
    private readonly StubSettings settings;
    private readonly IDeserializer deserializer;
    private readonly StubDefinitionValidator validator;
    private readonly StubDefinitionNormalizer normalizer;

    /// <summary>
    /// Creates a loader that discovers definitions relative to <see cref="IWebHostEnvironment.ContentRootPath"/> and uses the default <c>samples</c> search behavior when no explicit settings are supplied.
    /// </summary>
    /// <param name="environment">Supplies the content root used to locate the default definitions directory.</param>
    public StubDefinitionLoader(IWebHostEnvironment environment)
        : this(environment, Options.Create(new StubSettings()))
    {
    }

    /// <summary>
    /// Creates a loader that discovers definitions relative to <see cref="IWebHostEnvironment.ContentRootPath"/> and honors <see cref="StubSettings.DefinitionsPath"/> when configured.
    /// </summary>
    /// <param name="environment">Supplies the content root used as the starting point for relative definitions-path resolution.</param>
    /// <param name="settings">Supplies the optional definitions directory override. When unset, the nearest ancestor directory containing <c>samples</c> is used.</param>
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
    /// Discovers the active stub files, validates every document, normalizes repository-specific extensions, and merges compatible files into one deterministic view.
    /// </summary>
    /// <returns>A validated and normalized <see cref="StubDocument"/> suitable for request matching.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the configured definitions directory cannot be located.</exception>
    /// <exception cref="FileNotFoundException">Thrown when no supported stub files can be found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a stub file is malformed, violates validation rules, or cannot be merged with the other discovered files.</exception>
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
    /// Loads the bytes for a file-backed response payload as text using the same definitions root used for YAML discovery.
    /// </summary>
    /// <param name="fileName">An absolute path or a path relative to the active definitions directory.</param>
    /// <returns>The file contents without additional parsing or transformation.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the configured definitions directory cannot be located before resolving a relative path.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the requested file cannot be resolved.</exception>
    public string LoadResponseFileContent(string fileName)
    {
        var definitionsPath = ResolveDefinitionsDirectory();

        if (Path.IsPathRooted(fileName))
        {
            EnsurePathWithinDefinitionsDirectory(Path.GetFullPath(definitionsPath), Path.GetFullPath(fileName), fileName);
            return File.ReadAllText(fileName);
        }

        var path = ResolveSamplePath(fileName, definitionsPath);

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

    private string ResolveSamplePath(string fileName, string? resolvedDefinitionsPath = null)
    {
        var samplesPath = resolvedDefinitionsPath ?? ResolveDefinitionsDirectory();
        var root = Path.GetFullPath(samplesPath);
        var candidate = Path.GetFullPath(Path.Combine(samplesPath, fileName));

        EnsurePathWithinDefinitionsDirectory(root, candidate, fileName);

        if (File.Exists(candidate))
        {
            return candidate;
        }

        throw new FileNotFoundException(
            $"Could not locate {Path.Combine(GetDefinitionsPathLabel(), fileName)} from the current content root.",
            Path.Combine(GetDefinitionsPathLabel(), fileName));
    }

    private static void EnsurePathWithinDefinitionsDirectory(string root, string candidate, string originalPath)
    {
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !candidate.Equals(root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Response file path '{originalPath}' resolves outside the definitions directory.");
        }
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
