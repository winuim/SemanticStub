using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;
using SemanticStub.Application.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Resolution;

public sealed class StubResponseBuilderTests
{
    [Fact]
    public void TryBuild_UsesBodyContentForRelativeResponseFile()
    {
        var builder = new StubResponseBuilder(_ => "<root><item>1</item></root>");
        var responseDefinition = new ResponseDefinition
        {
            ResponseFile = "data.xml",
            Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
            {
                ["application/xml"] = new()
            }
        };

        var built = builder.TryBuild(200, responseDefinition, out var response);

        Assert.True(built);
        Assert.Equal("<root><item>1</item></root>", response.Body);
        Assert.Null(response.FilePath);
        Assert.Equal("application/xml", response.ContentType);
    }

    [Fact]
    public void TryBuild_UsesFilePathForAbsoluteResponseFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"semanticstub-{Guid.NewGuid():N}.txt");
        var builder = new StubResponseBuilder(_ => throw new InvalidOperationException("Reader should not be called for absolute paths."));
        var responseDefinition = new QueryMatchResponseDefinition
        {
            StatusCode = 200,
            ResponseFile = filePath,
            Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
            {
                ["text/plain"] = new()
            }
        };

        var built = builder.TryBuild(responseDefinition, out var response);

        Assert.True(built);
        Assert.Equal(string.Empty, response.Body);
        Assert.Equal(filePath, response.FilePath);
        Assert.Equal("text/plain", response.ContentType);
    }

    [Fact]
    public void TryBuild_ReturnsNonJsonStringExampleAsIs()
    {
        var builder = new StubResponseBuilder(_ => throw new InvalidOperationException("No file loading expected."));
        var responseDefinition = new ResponseDefinition
        {
            Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
            {
                ["text/plain"] = new()
                {
                    Example = "Hello, world!"
                }
            }
        };

        var built = builder.TryBuild(200, responseDefinition, out var response);

        Assert.True(built);
        Assert.Equal("Hello, world!", response.Body);
        Assert.Equal("text/plain", response.ContentType);
    }

    [Fact]
    public void TryBuild_WithPathTemplate_SubstitutesValue()
    {
        var builder = new StubResponseBuilder(_ => throw new InvalidOperationException());
        var responseDefinition = new ResponseDefinition
        {
            Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
            {
                ["application/json"] = new()
                {
                    Example = new Dictionary<object, object> { ["id"] = "{{path.id}}" }
                }
            }
        };
        var context = new TemplateSubstitutionContext(
            PathParameters: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["id"] = "42" },
            Query: new Dictionary<string, StringValues>(StringComparer.Ordinal),
            Headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Body: null);

        var built = builder.TryBuild(200, responseDefinition, out var response, context);

        Assert.True(built);
        Assert.Contains("\"42\"", response.Body);
    }

    [Fact]
    public void TryBuild_WithQueryTemplate_SubstitutesValue()
    {
        var builder = new StubResponseBuilder(_ => throw new InvalidOperationException());
        var responseDefinition = new ResponseDefinition
        {
            Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
            {
                ["application/json"] = new()
                {
                    Example = new Dictionary<object, object> { ["role"] = "{{query.role}}" }
                }
            }
        };
        var context = new TemplateSubstitutionContext(
            PathParameters: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Query: new Dictionary<string, StringValues>(StringComparer.Ordinal) { ["role"] = new StringValues("admin") },
            Headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Body: null);

        var built = builder.TryBuild(200, responseDefinition, out var response, context);

        Assert.True(built);
        Assert.Contains("\"admin\"", response.Body);
    }

    [Fact]
    public void TryBuild_WithHeaderTemplate_SubstitutesValue()
    {
        var builder = new StubResponseBuilder(_ => throw new InvalidOperationException());
        var responseDefinition = new ResponseDefinition
        {
            Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
            {
                ["application/json"] = new()
                {
                    Example = new Dictionary<object, object> { ["requestId"] = "{{header.X-Request-Id}}" }
                }
            }
        };
        var context = new TemplateSubstitutionContext(
            PathParameters: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Query: new Dictionary<string, StringValues>(StringComparer.Ordinal),
            Headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["X-Request-Id"] = "req-abc" },
            Body: null);

        var built = builder.TryBuild(200, responseDefinition, out var response, context);

        Assert.True(built);
        Assert.Contains("\"req-abc\"", response.Body);
    }

    [Fact]
    public void TryBuild_WithBodyTemplate_SubstitutesValue()
    {
        var builder = new StubResponseBuilder(_ => throw new InvalidOperationException());
        var responseDefinition = new ResponseDefinition
        {
            Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
            {
                ["application/json"] = new()
                {
                    Example = new Dictionary<object, object> { ["echo"] = "{{body.userId}}" }
                }
            }
        };
        var context = new TemplateSubstitutionContext(
            PathParameters: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Query: new Dictionary<string, StringValues>(StringComparer.Ordinal),
            Headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Body: """{"userId":"user-99"}""");

        var built = builder.TryBuild(200, responseDefinition, out var response, context);

        Assert.True(built);
        Assert.Contains("\"user-99\"", response.Body);
    }

    [Fact]
    public void TryBuild_WithMissingKey_LeavesPlaceholderIntact()
    {
        var builder = new StubResponseBuilder(_ => throw new InvalidOperationException());
        var responseDefinition = new ResponseDefinition
        {
            Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
            {
                ["application/json"] = new()
                {
                    Example = new Dictionary<object, object> { ["id"] = "{{path.id}}" }
                }
            }
        };
        var context = new TemplateSubstitutionContext(
            PathParameters: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Query: new Dictionary<string, StringValues>(StringComparer.Ordinal),
            Headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Body: null);

        var built = builder.TryBuild(200, responseDefinition, out var response, context);

        Assert.True(built);
        Assert.Contains("{{path.id}}", response.Body);
    }

    [Fact]
    public void TryBuild_WithAbsoluteResponseFile_SkipsSubstitution()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"semanticstub-{Guid.NewGuid():N}.bin");
        var builder = new StubResponseBuilder(_ => throw new InvalidOperationException("Reader should not be called."));
        var responseDefinition = new ResponseDefinition
        {
            ResponseFile = filePath,
            Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
            {
                ["application/octet-stream"] = new()
            }
        };
        var context = new TemplateSubstitutionContext(
            PathParameters: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["id"] = "42" },
            Query: new Dictionary<string, StringValues>(StringComparer.Ordinal),
            Headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Body: null);

        var built = builder.TryBuild(200, responseDefinition, out var response, context);

        Assert.True(built);
        Assert.Equal(filePath, response.FilePath);
    }
}
