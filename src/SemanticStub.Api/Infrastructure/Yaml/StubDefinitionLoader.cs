using System.Text.Json;
using SemanticStub.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemanticStub.Api.Infrastructure.Yaml;

public sealed class StubDefinitionLoader
{
    private const string DefaultStubFileName = "basic-routing.yaml";
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
        var yaml = File.ReadAllText(path);
        var document = deserializer.Deserialize<StubDocument>(yaml);

        if (document is null)
        {
            throw new InvalidOperationException("Failed to deserialize stub definition.");
        }

        return document;
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
