using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SemanticStub.Api.Tests.Integration;

public sealed class AutomaticReloadTests
{
    [Fact]
    public async Task GetHello_ReflectsUpdatedYamlWithoutRestart()
    {
        using var workspace = ReloadWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /hello:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: before
            """);
        using var factory = new ReloadingStubFactory(workspace.RootPath);
        using var client = factory.CreateClient();

        var initial = await client.GetFromJsonAsync<MessageResponse>("/hello");
        Assert.NotNull(initial);
        Assert.Equal("before", initial.Message);

        workspace.WriteDefaultDefinition(
            """
            openapi: 3.1.0
            paths:
              /hello:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: after
            """);

        var reloaded = await WaitForMessageAsync(client, "/hello", "after");
        Assert.Equal("after", reloaded);
    }

    [Fact]
    public async Task GetHello_KeepsLastKnownGoodDefinitionWhenReloadFails()
    {
        using var workspace = ReloadWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /hello:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: before
            """);
        using var factory = new ReloadingStubFactory(workspace.RootPath);
        using var client = factory.CreateClient();

        workspace.WriteDefaultDefinition(
            """
            openapi: 3.1.0
            paths:
              /hello:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: after
            """);

        var reloaded = await WaitForMessageAsync(client, "/hello", "after");
        Assert.Equal("after", reloaded);

        workspace.WriteDefaultDefinition(
            """
            paths:
              /hello:
                get:
            """);

        await Task.Delay(TimeSpan.FromSeconds(1));

        var response = await client.GetFromJsonAsync<MessageResponse>("/hello");
        Assert.NotNull(response);
        Assert.Equal("after", response.Message);
    }

    [Fact]
    public async Task GetCreatedStubRoute_ReflectsNewAdditionalStubFileWithoutRestart()
    {
        using var workspace = ReloadWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /hello:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: hello
            """);
        using var factory = new ReloadingStubFactory(workspace.RootPath);
        using var client = factory.CreateClient();

        var initialResponse = await client.GetAsync("/dynamic");
        Assert.Equal(HttpStatusCode.NotFound, initialResponse.StatusCode);

        workspace.WriteAdditionalStubFile(
            "dynamic.stub.yaml",
            """
            openapi: 3.1.0
            paths:
              /dynamic:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: dynamic
            """);

        var reloaded = await WaitForMessageAsync(client, "/dynamic", "dynamic");
        Assert.Equal("dynamic", reloaded);
    }

    private static async Task<string> WaitForMessageAsync(HttpClient client, string path, string expectedMessage)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(10);
        Exception? lastException = null;

        while (DateTime.UtcNow < timeoutAt)
        {
            try
            {
                var response = await client.GetAsync(path);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var payload = await response.Content.ReadFromJsonAsync<MessageResponse>();

                    if (payload?.Message == expectedMessage)
                    {
                        return payload.Message;
                    }
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for '{path}' to return message '{expectedMessage}'.", lastException);
    }

    private sealed class ReloadingStubFactory(string contentRootPath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(contentRootPath);
        }
    }

    private sealed class ReloadWorkspace(string rootPath) : IDisposable
    {
        public string RootPath { get; } = rootPath;

        public static ReloadWorkspace Create(string yaml)
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "semanticstub-reload-tests", Guid.NewGuid().ToString("N"));
            var samplesPath = Path.Combine(rootPath, "samples");
            Directory.CreateDirectory(samplesPath);
            File.WriteAllText(Path.Combine(samplesPath, "basic-routing.yaml"), yaml);
            return new ReloadWorkspace(rootPath);
        }

        public void WriteDefaultDefinition(string yaml)
        {
            File.WriteAllText(Path.Combine(RootPath, "samples", "basic-routing.yaml"), yaml);
        }

        public void WriteAdditionalStubFile(string fileName, string yaml)
        {
            File.WriteAllText(Path.Combine(RootPath, "samples", fileName), yaml);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private sealed class MessageResponse
    {
        public string Message { get; set; } = string.Empty;
    }
}
