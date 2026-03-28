using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SemanticStub.Api.Tests.Integration;

public sealed class BasicRoutingStubTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public BasicRoutingStubTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHello_ReturnsJsonExampleFromYaml()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/hello");
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("hello", response.Headers.GetValues("X-Stub-Source").Single());
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(200));

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
    public async Task GetDownload_ReturnsBinaryResponseFileWithoutStringConversion()
    {
        var response = await client.GetAsync("/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);

        var payload = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(new byte[] { 0x00, 0x01, 0x7F, 0xFF, 0x10 }, payload);
    }

    [Fact]
    public async Task GetUsersWithAdminRole_ReturnsAdminUsers()
    {
        var response = await client.GetAsync("/users?role=admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("admin", response.Headers.GetValues("X-User-Role").Single());
        var payload = await response.Content.ReadFromJsonAsync<UsersResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload.Users);
        Assert.Equal("Alice", payload.Users[0].Name);
        Assert.Equal("admin", payload.Users[0].Role);
        Assert.Equal(string.Empty, payload.Users[0].View);
    }

    [Fact]
    public async Task GetUsersWithAdminRoleAndSummaryView_PrefersMoreSpecificMatch()
    {
        var response = await client.GetAsync("/users?role=admin&view=summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<UsersResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload.Users);
        Assert.Equal("Alice", payload.Users[0].Name);
        Assert.Equal("admin", payload.Users[0].Role);
        Assert.Equal("summary", payload.Users[0].View);
    }

    [Fact]
    public async Task GetUsersWithGuestRole_ReturnsGuestUsers()
    {
        var response = await client.GetAsync("/users?role=guest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<UsersResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload.Users);
        Assert.Equal("Bob", payload.Users[0].Name);
        Assert.Equal("guest", payload.Users[0].Role);
    }

    [Fact]
    public async Task GetUsersWithUnknownRole_ReturnsDefaultUsers()
    {
        var response = await client.GetAsync("/users?role=other");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<UsersResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Users.Count);
    }

    [Fact]
    public async Task GetUsersWithNoMatchingQueryAndNoWildcard_UsesResponsesFallback()
    {
        var response = await client.GetAsync("/users?view=compact");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<UsersResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Users.Count);
        Assert.Equal("Alice", payload.Users[0].Name);
        Assert.Equal("Bob", payload.Users[1].Name);
    }

    [Fact]
    public async Task GetUsersWithGuestRoleAndExtraQueryParameter_ReturnsGuestUsers()
    {
        var response = await client.GetAsync("/users?role=guest&view=summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<UsersResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload.Users);
        Assert.Equal("Bob", payload.Users[0].Name);
        Assert.Equal("guest", payload.Users[0].Role);
    }

    [Fact]
    public async Task GetUsersWithPartialRoleQuery_ReturnsPartialMatchResponse()
    {
        var response = await client.GetAsync("/users?role=super-admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<UsersResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload.Users);
        Assert.Equal("Partial Alice", payload.Users[0].Name);
        Assert.Equal("partial-admin", payload.Users[0].Role);
    }

    [Fact]
    public async Task GetSearchWithOrderedRepeatedTagQuery_ReturnsSpecificResponse()
    {
        var response = await client.GetAsync("/search?tag=alpha&tag=beta");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(payload);
        Assert.Equal("ordered-tags", payload.Result);
    }

    [Fact]
    public async Task GetSearchWithDifferentRepeatedTagQueryOrder_UsesFallbackResponse()
    {
        var response = await client.GetAsync("/search?tag=beta&tag=alpha");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(payload);
        Assert.Equal("default-search", payload.Result);
    }

    [Fact]
    public async Task GetRegexUsersWithMatchingRole_ReturnsRegexSpecificResponse()
    {
        var response = await client.GetAsync("/regex-users?role=admin-42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<UsersResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload.Users);
        Assert.Equal("Regex Alice", payload.Users[0].Name);
        Assert.Equal("regex-admin", payload.Users[0].Role);
    }

    [Fact]
    public async Task GetRegexUsersWithNonMatchingRole_ReturnsFallbackResponse()
    {
        var response = await client.GetAsync("/regex-users?role=guest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<UsersResponse>();
        Assert.NotNull(payload);
        Assert.Empty(payload.Users);
    }

    [Fact]
    public async Task GetUsersWithEnvironmentHeader_ReturnsHeaderSpecificUsers()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/environment-users");
        request.Headers.Add("X-Env", "staging");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<UsersResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload.Users);
        Assert.Equal("Staging Alice", payload.Users[0].Name);
        Assert.Equal("staging", payload.Users[0].Role);
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
        Assert.Equal("demo-token", payload.Token);
    }

    [Fact]
    public async Task PostLogin_WithUnknownCredentials_ReturnsDefaultResponse()
    {
        var response = await client.PostAsJsonAsync("/login", new LoginRequest
        {
            Username = "other",
            Password = "wrong"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.Equal("invalid", payload.Result);
        Assert.Null(payload.Token);
    }

    [Fact]
    public async Task PostCheckout_AdvancesScenarioStateAcrossRequests()
    {
        var firstResponse = await client.PostAsync("/checkout", content: null);
        var secondResponse = await client.PostAsync("/checkout", content: null);

        Assert.Equal(HttpStatusCode.Conflict, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<LoginResponse>();
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(firstPayload);
        Assert.NotNull(secondPayload);
        Assert.Equal("pending", firstPayload.Result);
        Assert.Equal("complete", secondPayload.Result);
    }

    [Fact]
    public async Task PutProfile_ReturnsConfiguredResponse()
    {
        var response = await client.PutAsJsonAsync("/profile", new ProfileRequest
        {
            DisplayName = "Updated User"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<MutationResponse>();
        Assert.NotNull(payload);
        Assert.Equal("replaced", payload.Result);
    }

    [Fact]
    public async Task PatchProfile_WithMatchingBody_ReturnsSpecificResponse()
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, "/profile")
        {
            Content = JsonContent.Create(new ProfilePatchRequest
            {
                Nickname = "stubby"
            })
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<MutationResponse>();
        Assert.NotNull(payload);
        Assert.Equal("patched-specific", payload.Result);
    }

    [Fact]
    public async Task PatchProfile_WithNonMatchingBody_ReturnsFallbackResponse()
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, "/profile")
        {
            Content = JsonContent.Create(new ProfilePatchRequest
            {
                Nickname = "other"
            })
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<MutationResponse>();
        Assert.NotNull(payload);
        Assert.Equal("patched", payload.Result);
    }

    [Fact]
    public async Task DeleteProfile_ReturnsConfiguredResponse()
    {
        var response = await client.DeleteAsync("/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<MutationResponse>();
        Assert.NotNull(payload);
        Assert.Equal("deleted", payload.Result);
    }

    [Fact]
    public async Task GetLogin_ReturnsMethodNotAllowed()
    {
        var response = await client.GetAsync("/login");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("POST", string.Join(", ", response.Content.Headers.Allow));
    }

    [Fact]
    public async Task PostHello_ReturnsMethodNotAllowed()
    {
        var response = await client.PostAsync("/hello", content: null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_ReturnsMethodNotAllowed()
    {
        var response = await client.GetAsync("/profile");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("PUT, PATCH, DELETE", string.Join(", ", response.Content.Headers.Allow));
    }

    [Fact]
    public async Task GetOrderByTemplatePath_ReturnsPatternRouteResponse()
    {
        var response = await client.GetAsync("/orders/123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<MutationResponse>();
        Assert.NotNull(payload);
        Assert.Equal("pattern", payload.Result);
    }

    [Fact]
    public async Task GetSpecialOrder_PrefersExactRouteOverTemplatePath()
    {
        var response = await client.GetAsync("/orders/special");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<MutationResponse>();
        Assert.NotNull(payload);
        Assert.Equal("exact", payload.Result);
    }

    [Fact]
    public async Task PostOrderByTemplatePath_ReturnsMethodNotAllowed()
    {
        var response = await client.PostAsync("/orders/123", content: null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("GET", string.Join(", ", response.Content.Headers.Allow));
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

        public string Role { get; init; } = string.Empty;

        public string View { get; init; } = string.Empty;
    }

    public sealed class LoginRequest
    {
        public string Username { get; init; } = string.Empty;

        public string Password { get; init; } = string.Empty;
    }

    public sealed class LoginResponse
    {
        public string Result { get; init; } = string.Empty;

        public string? Token { get; init; }
    }

    public sealed class ProfileRequest
    {
        public string DisplayName { get; init; } = string.Empty;
    }

    public sealed class ProfilePatchRequest
    {
        public string Nickname { get; init; } = string.Empty;
    }

    public sealed class MutationResponse
    {
        public string Result { get; init; } = string.Empty;
    }

    public sealed class SearchResponse
    {
        public string Result { get; init; } = string.Empty;
    }
}
