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

    [Fact]
    public async Task GetRoute_ReturnsDetailedRoutePayload()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/routes/listUsers");
        response.EnsureSuccessStatusCode();

        var route = await response.Content.ReadFromJsonAsync<StubRouteDetailInfo>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(route);
        Assert.Equal("listUsers", route!.RouteId);
        Assert.Equal("GET", route.Method);
        Assert.Equal("/users", route.PathPattern);
        Assert.True(route.HasConditionalMatches);
        Assert.True(route.ResponseCount >= 1);
        Assert.Contains(route.Responses, response => response.ResponseId == "200");
        Assert.Contains(route.ConditionalMatches, candidate => candidate.HasExactQuery);
        Assert.Contains(route.ConditionalMatches, candidate => candidate.HasPartialQuery);
    }

    [Fact]
    public async Task GetRoute_ReturnsScenarioMetadata_WhenConfigured()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/routes/checkout");
        response.EnsureSuccessStatusCode();

        var route = await response.Content.ReadFromJsonAsync<StubRouteDetailInfo>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(route);
        Assert.Equal("checkout", route!.RouteId);
        Assert.True(route.UsesScenario);
        Assert.False(route.HasConditionalMatches);
        Assert.Contains(route.Responses, response =>
            response.ResponseId == "409"
            && response.UsesScenario
            && response.Scenario is not null
            && response.Scenario.Name == "checkout-flow"
            && response.Scenario.State == "initial"
            && response.Scenario.Next == "confirmed");
    }

    [Fact]
    public async Task GetRoute_ReturnsNotFound_WhenRouteDoesNotExist()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/routes/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetScenarios_ReturnsOk()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/scenarios");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetScenarios_ResponseDeserializesToScenarioStateInfoArray()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/scenarios");
        response.EnsureSuccessStatusCode();

        var scenarios = await response.Content.ReadFromJsonAsync<ScenarioStateInfo[]>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(scenarios);
    }

    [Fact]
    public async Task ResetScenarios_ReturnsNoContent()
    {
        var response = await client.PostAsync("/_semanticstub/runtime/scenarios/reset", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ResetScenario_ReturnsNotFound_WhenScenarioDoesNotExist()
    {
        var response = await client.PostAsync("/_semanticstub/runtime/scenarios/does-not-exist/reset", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TestMatch_ReturnsSimulationPayload()
    {
        var response = await client.PostAsJsonAsync("/_semanticstub/runtime/test-match", new MatchRequestInfo
        {
            Method = "GET",
            Path = "/hello"
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MatchSimulationInfo>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(payload);
        Assert.True(payload.Matched);
        Assert.Equal("Matched", payload.MatchResult);
        Assert.Equal("getHello", payload.RouteId);
    }

    [Fact]
    public async Task ExplainMatch_ReturnsExplanationPayload()
    {
        var response = await client.PostAsJsonAsync("/_semanticstub/runtime/explain", new MatchRequestInfo
        {
            Method = "GET",
            Path = "/users",
            Query = new Dictionary<string, string[]>
            {
                ["role"] = ["admin"]
            },
            IncludeCandidates = true
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MatchExplanationInfo>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(payload);
        Assert.True(payload.PathMatched);
        Assert.True(payload.MethodMatched);
        Assert.Equal("Matched", payload.Result.MatchResult);
        Assert.NotEmpty(payload.DeterministicCandidates);
    }

    [Fact]
    public async Task ExplainLastMatch_ReturnsMostRecentRealRequestExplanation()
    {
        var routedResponse = await client.GetAsync("/users?role=admin");
        routedResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/_semanticstub/runtime/explain/last");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MatchExplanationInfo>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(payload);
        Assert.True(payload!.PathMatched);
        Assert.True(payload.MethodMatched);
        Assert.Equal("Matched", payload.Result.MatchResult);
        Assert.Equal("listUsers", payload.Result.RouteId);
        Assert.NotEmpty(payload.Result.Candidates);
    }

    [Fact]
    public async Task ExplainLastMatch_IsNotOverwrittenByFailedProbe()
    {
        var matchedResponse = await client.GetAsync("/users?role=admin");
        matchedResponse.EnsureSuccessStatusCode();

        var failedProbe = await client.GetAsync("/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, failedProbe.StatusCode);

        var response = await client.GetAsync("/_semanticstub/runtime/explain/last");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MatchExplanationInfo>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(payload);
        Assert.Equal("Matched", payload!.Result.MatchResult);
        Assert.Equal("listUsers", payload.Result.RouteId);
    }
}
