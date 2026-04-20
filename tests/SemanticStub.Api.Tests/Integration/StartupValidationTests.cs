using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace SemanticStub.Api.Tests.Integration;

public sealed class StartupValidationTests
{
    [Fact]
    public void CreateClient_ThrowsWhenSemanticMatchingEnabledWithEmptyEndpoint()
    {
        using var factory = new SettingsFactory([
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:Enabled", "true"),
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:Endpoint", ""),
        ]);

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("non-empty absolute HTTP or HTTPS URI", exception.ToString());
    }

    [Fact]
    public void CreateClient_ThrowsWhenSemanticMatchingEnabledWithRelativeEndpoint()
    {
        using var factory = new SettingsFactory([
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:Enabled", "true"),
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:Endpoint", "relative/path"),
        ]);

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("non-empty absolute HTTP or HTTPS URI", exception.ToString());
    }

    [Fact]
    public void CreateClient_ThrowsWhenSemanticMatchingEnabledWithUnsupportedScheme()
    {
        using var factory = new SettingsFactory([
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:Enabled", "true"),
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:Endpoint", "ftp://host/embed"),
        ]);

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("non-empty absolute HTTP or HTTPS URI", exception.ToString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void CreateClient_ThrowsWhenTimeoutSecondsIsNotPositive(string value)
    {
        using var factory = new SettingsFactory([
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:TimeoutSeconds", value),
        ]);

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("must be positive", exception.ToString());
    }

    [Fact]
    public void CreateClient_ThrowsWhenThresholdIsOutOfRange()
    {
        using var factory = new SettingsFactory([
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:Threshold", "1.5"),
        ]);

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("cosine similarity range", exception.ToString());
    }

    [Fact]
    public void CreateClient_ThrowsWhenTopScoreMarginIsNegative()
    {
        using var factory = new SettingsFactory([
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:TopScoreMargin", "-0.1"),
        ]);

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("non-negative", exception.ToString());
    }

    [Fact]
    public void CreateClient_SucceedsWhenSemanticMatchingEnabledWithValidAbsoluteEndpoint()
    {
        using var factory = new SettingsFactory([
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:Enabled", "true"),
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:Endpoint", "http://localhost:8080"),
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:TimeoutSeconds", "30"),
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:Threshold", "0.85"),
            KeyValuePair.Create<string, string?>("StubSettings:SemanticMatching:TopScoreMargin", "0"),
        ]);

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.Null(exception);
    }

    [Fact]
    public void CreateClient_ThrowsWhenStubDefinitionIsInvalidAtStartup()
    {
        using var workspace = InvalidStubWorkspace.Create(
            """
            paths:
              /broken:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: broken
            """);

        using var factory = new InvalidStubFactory(workspace.RootPath);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("The 'openapi' field is required.", exception.ToString());
    }

    private sealed class SettingsFactory(IEnumerable<KeyValuePair<string, string?>> config)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(config));
        }
    }

    private sealed class InvalidStubFactory(string contentRootPath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(contentRootPath);
        }
    }

    private sealed class InvalidStubWorkspace(string rootPath) : IDisposable
    {
        public string RootPath { get; } = rootPath;

        public static InvalidStubWorkspace Create(string yaml)
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "semanticstub-startup-tests", Guid.NewGuid().ToString("N"));
            var samplesPath = Path.Combine(rootPath, "samples");
            Directory.CreateDirectory(samplesPath);
            File.WriteAllText(Path.Combine(samplesPath, "basic-routing.yaml"), yaml);

            return new InvalidStubWorkspace(rootPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
