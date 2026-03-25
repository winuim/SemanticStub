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

    [Fact]
    public async Task GetUsers_ReturnsJsonFromResponseFile()
    {
        var response = await client.GetAsync("/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var payload = await response.Content.ReadFromJsonAsync<UsersResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Users.Count);
        Assert.Equal("Alice", payload.Users[0].Name);
        Assert.Equal("Bob", payload.Users[1].Name);
    }

    [Fact]
    public async Task PostLogin_ReturnsJsonExampleFromYaml()
    {
        var response = await client.PostAsJsonAsync("/login", new LoginRequest
        {
            Username = "demo",
            Password = "secret"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.Equal("ok", payload.Result);
    }

    [Fact]
    public async Task GetLogin_ReturnsMethodNotAllowed()
    {
        var response = await client.GetAsync("/login");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task PostHello_ReturnsMethodNotAllowed()
    {
        var response = await client.PostAsync("/hello", content: null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    public sealed class HelloResponse
    {
        public string Message { get; init; } = string.Empty;
    }

    public sealed class UsersResponse
    {
        public List<UserResponse> Users { get; init; } = [];
    }

    public sealed class UserResponse
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    public sealed class LoginRequest
    {
        public string Username { get; init; } = string.Empty;

        public string Password { get; init; } = string.Empty;
    }

    public sealed class LoginResponse
    {
        public string Result { get; init; } = string.Empty;
    }
}
