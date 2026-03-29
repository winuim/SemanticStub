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

    [Fact]
    public async Task PostCheckout_ResetsScenarioStateAfterReload()
    {
        using var workspace = ReloadWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /checkout:
                post:
                  responses:
                    "409":
                      description: pending
                      x-scenario:
                        name: checkout-flow
                        state: initial
                        next: confirmed
                      content:
                        application/json:
                          example:
                            result: pending
                    "200":
                      description: complete
                      x-scenario:
                        name: checkout-flow
                        state: confirmed
                      content:
                        application/json:
                          example:
                            result: complete
            """);
        using var factory = new ReloadingStubFactory(workspace.RootPath);
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsync("/checkout", new StringContent(string.Empty));
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<ResultResponse>();
        Assert.Equal(HttpStatusCode.Conflict, firstResponse.StatusCode);
        Assert.NotNull(firstPayload);
        Assert.Equal("pending", firstPayload.Result);

        var secondResponse = await client.PostAsync("/checkout", new StringContent(string.Empty));
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<ResultResponse>();
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondPayload);
        Assert.Equal("complete", secondPayload.Result);

        workspace.WriteDefaultDefinition(
            """
            openapi: 3.1.0
            paths:
              /checkout:
                post:
                  responses:
                    "409":
                      description: pending
                      x-scenario:
                        name: checkout-flow
                        state: initial
                        next: confirmed
                      content:
                        application/json:
                          example:
                            result: reloaded-pending
                    "200":
                      description: complete
                      x-scenario:
                        name: checkout-flow
                        state: confirmed
                      content:
                        application/json:
                          example:
                            result: reloaded-complete
            """);

        var reloadedResult = await WaitForResultAsync(client, "/checkout", "reloaded-pending");

        Assert.Equal(HttpStatusCode.Conflict, reloadedResult.StatusCode);
        Assert.Equal("reloaded-pending", reloadedResult.Result);
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

    private static async Task<(HttpStatusCode StatusCode, string Result)> WaitForResultAsync(HttpClient client, string path, string expectedResult)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(10);
        Exception? lastException = null;

        while (DateTime.UtcNow < timeoutAt)
        {
            try
            {
                var response = await client.PostAsync(path, new StringContent(string.Empty));
                var payload = await response.Content.ReadFromJsonAsync<ResultResponse>();

                if (payload?.Result == expectedResult)
                {
                    return (response.StatusCode, payload.Result);
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for '{path}' to return result '{expectedResult}'.", lastException);
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

    private sealed class ResultResponse
    {
        public string Result { get; set; } = string.Empty;
    }
}
