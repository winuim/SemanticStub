using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        Assert.Contains(route.Responses, response => response.ResponseId == "200" && response.ResponseFile == "users.json");
        Assert.Contains(route.Responses, response => response.MediaTypes.Contains("application/json"));
        Assert.Contains(route.ConditionalMatches, candidate => candidate.HasExactQuery);
        Assert.Contains(route.ConditionalMatches, candidate => candidate.MediaTypes.Contains("application/json"));
        Assert.Contains(route.ConditionalMatches, candidate => candidate.ResponseFile is null);
        Assert.Contains(route.ConditionalMatches, candidate => candidate.HasRegexQuery);
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
            && response.Scenario.AdvancesScenarioState
            && response.Scenario.Next == "confirmed");
    }

    [Fact]
    public async Task GetRoute_ReturnsNotFound_WhenRouteDoesNotExist()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/routes/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertNotFoundProblemDetails(response, "Route not found");
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
    public async Task GetMetrics_ReturnsOk()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMetrics_ResponseDeserializesToRuntimeMetricsSummaryInfo()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/metrics");
        response.EnsureSuccessStatusCode();

        var metrics = await response.Content.ReadFromJsonAsync<RuntimeMetricsSummaryInfo>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(metrics);
        Assert.True(metrics!.TotalRequestCount >= 0);
        Assert.True(metrics.MatchedRequestCount >= 0);
        Assert.True(metrics.UnmatchedRequestCount >= 0);
    }

    [Fact]
    public async Task GetMetrics_ReflectsRealRequestsHandledByStubController()
    {
        var beforeResponse = await client.GetAsync("/_semanticstub/runtime/metrics");
        beforeResponse.EnsureSuccessStatusCode();
        var before = await beforeResponse.Content.ReadFromJsonAsync<RuntimeMetricsSummaryInfo>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var routedResponse = await client.GetAsync("/hello");
        routedResponse.EnsureSuccessStatusCode();

        var afterResponse = await client.GetAsync("/_semanticstub/runtime/metrics");
        afterResponse.EnsureSuccessStatusCode();
        var after = await afterResponse.Content.ReadFromJsonAsync<RuntimeMetricsSummaryInfo>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal(before!.TotalRequestCount + 1, after!.TotalRequestCount);
        Assert.True(after.MatchedRequestCount >= before.MatchedRequestCount + 1);
        Assert.Contains(after.StatusCodes, entry => entry.StatusCode == (int)HttpStatusCode.OK);
        Assert.Contains(after.TopRoutes, entry => entry.RouteId == "getHello");
    }

    [Fact]
    public async Task ResetMetrics_ReturnsNoContent()
    {
        var response = await client.PostAsync("/_semanticstub/runtime/metrics/reset", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ResetMetricsAlias_ReturnsNoContent()
    {
        var response = await client.PostAsync("/_semanticstub/runtime/metrics/resets", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ResetMetrics_ClearsMetricsAndRecentRequests()
    {
        var routedResponse = await client.GetAsync("/users?role=admin");
        routedResponse.EnsureSuccessStatusCode();

        var resetResponse = await client.PostAsync("/_semanticstub/runtime/metrics/reset", content: null);
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        var metricsResponse = await client.GetAsync("/_semanticstub/runtime/metrics");
        metricsResponse.EnsureSuccessStatusCode();
        var metrics = await metricsResponse.Content.ReadFromJsonAsync<RuntimeMetricsSummaryInfo>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var requestsResponse = await client.GetAsync("/_semanticstub/runtime/requests");
        requestsResponse.EnsureSuccessStatusCode();
        var requests = await requestsResponse.Content.ReadFromJsonAsync<RecentRequestInfo[]>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(metrics);
        Assert.Equal(0, metrics!.TotalRequestCount);
        Assert.Equal(0, metrics.MatchedRequestCount);
        Assert.Equal(0, metrics.UnmatchedRequestCount);
        Assert.Empty(metrics.StatusCodes);
        Assert.Empty(metrics.TopRoutes);
        Assert.NotNull(requests);
        Assert.Empty(requests!);
    }

    [Fact]
    public async Task ResetMetricsAlias_ClearsMetricsAndRecentRequests()
    {
        var routedResponse = await client.GetAsync("/users?role=admin");
        routedResponse.EnsureSuccessStatusCode();

        var resetResponse = await client.PostAsync("/_semanticstub/runtime/metrics/resets", content: null);
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        var metricsResponse = await client.GetAsync("/_semanticstub/runtime/metrics");
        metricsResponse.EnsureSuccessStatusCode();
        var metrics = await metricsResponse.Content.ReadFromJsonAsync<RuntimeMetricsSummaryInfo>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var requestsResponse = await client.GetAsync("/_semanticstub/runtime/requests");
        requestsResponse.EnsureSuccessStatusCode();
        var requests = await requestsResponse.Content.ReadFromJsonAsync<RecentRequestInfo[]>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(metrics);
        Assert.Equal(0, metrics!.TotalRequestCount);
        Assert.Equal(0, metrics.MatchedRequestCount);
        Assert.Equal(0, metrics.UnmatchedRequestCount);
        Assert.Empty(metrics.StatusCodes);
        Assert.Empty(metrics.TopRoutes);
        Assert.NotNull(requests);
        Assert.Empty(requests!);
    }

    [Fact]
    public async Task GetRecentRequests_ReturnsOk()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRecentRequests_ResponseDeserializesToRecentRequestInfoArray()
    {
        var response = await client.GetAsync("/_semanticstub/runtime/requests");
        response.EnsureSuccessStatusCode();

        var requests = await response.Content.ReadFromJsonAsync<RecentRequestInfo[]>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(requests);
    }

    [Fact]
    public async Task GetRecentRequests_ReflectsNewestRealRequest_AndRespectsLimit()
    {
        var routedResponse = await client.GetAsync("/users?role=admin");
        routedResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/_semanticstub/runtime/requests?limit=1");
        response.EnsureSuccessStatusCode();

        var requests = await response.Content.ReadFromJsonAsync<RecentRequestInfo[]>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(requests);
        var request = Assert.Single(requests!);
        Assert.Equal(HttpMethods.Get, request.Method);
        Assert.Equal("/users", request.Path);
        Assert.Equal("listUsers", request.RouteId);
        Assert.Equal((int)HttpStatusCode.OK, request.StatusCode);
    }

    [Fact]
    public async Task ResetScenarios_ReturnsNoContent()
    {
        var response = await client.PostAsync("/_semanticstub/runtime/scenarios/reset", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ResetScenariosAlias_ReturnsNoContent()
    {
        var response = await client.PostAsync("/_semanticstub/runtime/scenarios/resets", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ResetScenarioAlias_ReturnsNoContent_WhenScenarioExists()
    {
        var response = await client.PostAsync("/_semanticstub/runtime/scenarios/checkout-flow/resets", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ResetScenario_ReturnsNotFound_WhenScenarioDoesNotExist()
    {
        var response = await client.PostAsync("/_semanticstub/runtime/scenarios/does-not-exist/reset", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertNotFoundProblemDetails(response, "Scenario not found");
    }

    [Fact]
    public async Task ResetScenarioAlias_ReturnsNotFound_WhenScenarioDoesNotExist()
    {
        var response = await client.PostAsync("/_semanticstub/runtime/scenarios/does-not-exist/resets", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertNotFoundProblemDetails(response, "Scenario not found");
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

    [Fact]
    public async Task ExplainLastMatch_ReturnsNotFoundProblemDetails_WhenNoRealRequestExplanationExists()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var freshClient = factory.CreateClient();

        var response = await freshClient.GetAsync("/_semanticstub/runtime/explain/last");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertNotFoundProblemDetails(response, "Last match explanation not found");
    }

    private static async Task AssertNotFoundProblemDetails(HttpResponseMessage response, string expectedTitle)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status404NotFound, problem!.Status);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }
}
