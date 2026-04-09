using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

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
}
