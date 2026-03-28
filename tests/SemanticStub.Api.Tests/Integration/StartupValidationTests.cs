using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SemanticStub.Api.Tests.Integration;

public sealed class StartupValidationTests
{
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
