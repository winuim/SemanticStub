using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SemanticStub.Api.Tests.Integration;

public sealed class HelloWorldStubTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public HelloWorldStubTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHello_ReturnsJsonExampleFromYaml()
    {
        var response = await client.GetAsync("/hello");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var payload = await response.Content.ReadFromJsonAsync<HelloResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Hello from SemanticStub", payload.Message);
    }

    [Fact]
    public async Task GetUnknownRoute_ReturnsNotFound()
    {
        var response = await client.GetAsync("/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public sealed class HelloResponse
    {
        public string Message { get; init; } = string.Empty;
    }
}
