using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SemanticStub.Api.Extensions;
using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Integration;

public sealed class StubServiceCollectionExtensionsTests
{
    [Fact]
    public void AddStubServices_RegistersProductionCompositionGraph()
    {
        using var workspace = StubWorkspace.Create();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(Options.Create(new StubSettings { DefinitionsPath = workspace.SamplesPath }));
        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment
        {
            ApplicationName = "SemanticStub.Api.Tests",
            ContentRootPath = workspace.RootPath,
            ContentRootFileProvider = new PhysicalFileProvider(workspace.RootPath),
            EnvironmentName = Environments.Development,
            WebRootPath = workspace.RootPath,
            WebRootFileProvider = new PhysicalFileProvider(workspace.RootPath)
        });
        services.AddStubServices();

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.NotNull(serviceProvider.GetRequiredService<IStubService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IStubInspectionService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IStubDefinitionLoader>());
        Assert.NotEmpty(serviceProvider.GetServices<IHostedService>());
    }

    private sealed class StubWorkspace(string rootPath, string samplesPath) : IDisposable
    {
        public string RootPath { get; } = rootPath;

        public string SamplesPath { get; } = samplesPath;

        public static StubWorkspace Create()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "semanticstub-di-tests", Guid.NewGuid().ToString("N"));
            var samplesPath = Path.Combine(rootPath, "samples");
            Directory.CreateDirectory(samplesPath);
            File.WriteAllText(
                Path.Combine(samplesPath, "basic-routing.yaml"),
                """
                openapi: 3.1.0
                info:
                  title: DI Composition Test
                  version: 1.0.0
                paths:
                  /health:
                    get:
                      responses:
                        "200":
                          description: ok
                          content:
                            application/json:
                              example:
                                status: ok
                """);

            return new StubWorkspace(rootPath, samplesPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = null!;

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
