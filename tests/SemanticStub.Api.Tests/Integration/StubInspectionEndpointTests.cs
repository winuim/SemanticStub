using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SemanticStub.Api.Inspection;
using Xunit;

namespace SemanticStub.Api.Tests.Integration;

public sealed class StubInspectionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public StubInspectionEndpointTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetConfig_ReturnsOk()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetConfig_ResponseDeserializesToStubConfigSnapshot()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/config");
        response.EnsureSuccessStatusCode();

        var snapshot = await response.Content.ReadFromJsonAsync<StubConfigSnapshot>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(snapshot);
        Assert.NotEmpty(snapshot.ConfigurationHash);
        Assert.NotEmpty(snapshot.DefinitionsDirectoryPath);
        Assert.True(snapshot.RouteCount >= 0);
        Assert.Equal(TimeSpan.Zero, snapshot.SnapshotTimestamp.Offset);
    }

    [Fact]
    public async Task GetConfig_IsNotAbsorbedByCatchAllRoute()
    {
        // Verify the inspection endpoint is not swallowed by StubController's {*path} catch-all.
        // A 200 with a deserializable StubConfigSnapshot proves the inspection controller handled it.
        var response = await client.GetAsync("/_semanticstub/runtime/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetRoutes_ReturnsOk()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/routes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRoutes_ResponseIsJsonArray()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/routes");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetRoutes_ArrayElementsDeserializeToStubRouteInfo()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/routes");
        response.EnsureSuccessStatusCode();

        var routes = await response.Content.ReadFromJsonAsync<StubRouteInfo[]>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(routes);
        // The sample definitions include at least one route.
        Assert.NotEmpty(routes);
        foreach (var route in routes)
        {
            Assert.NotEmpty(route.RouteId);
            Assert.NotEmpty(route.Method);
            Assert.NotEmpty(route.PathPattern);
        }
    }
}
