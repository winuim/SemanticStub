using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Inspection;
using SemanticStub.Application.Infrastructure.Yaml;
using SemanticStub.Infrastructure.Yaml;
using SemanticStub.Api.Models;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services;
using SemanticStub.Application.Services.Semantic;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Inspection;

public sealed class StubInspectionRuntimeMetricsTests
{
    private static MatcherService CreateMatcherService()
    {
        return new MatcherService(new JsonBodyMatcher(), new FormBodyMatcher(), new QueryValueMatcher(), new RegexQueryMatcher());
    }

    // ---------------------------------------------------------------------------
    // Test helpers
    // ---------------------------------------------------------------------------

    private sealed class TestStubDefinitionLoader(StubDocument document, string directoryPath = "/test/definitions") : IStubDefinitionLoader
    {
        public string GetDefinitionsDirectoryPath() => directoryPath;
        public StubDocument LoadDefaultDefinition() => document;
        public string LoadResponseFileContent(string fileName) => throw new InvalidOperationException("Not used in inspection tests");
    }

    private sealed class ReloadingStubDefinitionLoader(StubDocument document, string directoryPath = "/test/definitions") : IStubDefinitionLoader
    {
        public StubDocument CurrentDocument { get; set; } = document;

        public string GetDefinitionsDirectoryPath() => directoryPath;

        public StubDocument LoadDefaultDefinition() => CurrentDocument;

        public string LoadResponseFileContent(string fileName) => throw new InvalidOperationException("Not used in inspection tests");
    }

    private static StubDefinitionState CreateState(StubDocument document, string directoryPath = "/test/definitions")
    {
        var loader = new TestStubDefinitionLoader(document, directoryPath);
        return new StubDefinitionState(loader, new ScenarioService(), NullLogger<StubDefinitionState>.Instance);
    }

    private static IStubInspectionService CreateService(
        StubDocument document,
        string directoryPath = "/test/definitions",
        bool semanticMatchingEnabled = false,
        MatcherService? matcherService = null,
        ISemanticMatcherService? semanticMatcherService = null)
    {
        var loader = new TestStubDefinitionLoader(document, directoryPath);
        var scenarioService = new ScenarioService();
        var state = new StubDefinitionState(loader, scenarioService, NullLogger<StubDefinitionState>.Instance);
        var settings = Options.Create(new StubSettings
        {
            SemanticMatching = new SemanticMatchingSettings { Enabled = semanticMatchingEnabled },
        });
        var stubService = CreateStubService(
            state,
            scenarioService,
            matcherService ?? CreateMatcherService(),
            semanticMatcherService ?? new NoOpSemanticMatcherService());
        return new StubInspectionService(
            state,
            loader,
            settings,
            stubService,
            new StubInspectionRuntimeStore(),
            new StubInspectionScenarioCoordinator(state, scenarioService));
    }

    private static StubService CreateStubService(
        StubDefinitionState state,
        ScenarioService scenarioService,
        MatcherService matcherService,
        ISemanticMatcherService semanticMatcherService)
    {
        var responseBuilder = new StubResponseBuilder(state.LoadResponseFileContent);

        return new StubService(
            state,
            matcherService,
            scenarioService,
            new StubDispatchSelector(
                matcherService,
                semanticMatcherService,
                responseBuilder,
                new StubDefaultResponseSelector(responseBuilder, scenarioService),
                scenarioService,
                NullLogger<StubDispatchSelector>.Instance),
            new StubInspectionProjectionBuilder(scenarioService));
    }

    private static StubDocument EmptyDocument() => new StubDocument
    {
        Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal),
    };

    private static StubDocument SingleGetDocument(string path = "/hello", string operationId = "sayHello") =>
        new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                [path] = new()
                {
                    Get = new OperationDefinition
                    {
                        OperationId = operationId,
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new() { Description = "OK" },
                        },
                    },
                },
            },
        };

    private static MatchExplanationInfo CreateRecordedExplanation(
        bool matched,
        string matchResult,
        string? routeId = null,
        string? matchMode = null)
    {
        return new MatchExplanationInfo
        {
            Result = new MatchSimulationInfo
            {
                Matched = matched,
                MatchResult = matchResult,
                RouteId = routeId,
                MatchMode = matchMode,
            }
        };
    }

    private sealed class NoOpSemanticMatcherService : ISemanticMatcherService
    {
        public Task<SemanticMatchExplanation> ExplainMatchAsync(
            string method,
            string path,
            IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> query,
            IReadOnlyDictionary<string, string> headers,
            string? body,
            IReadOnlyCollection<QueryMatchDefinition> candidates,
            Func<QueryMatchDefinition, bool>? candidateFilter = null,
            bool includeCandidateScores = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SemanticMatchExplanation());
        }
    }

    private sealed class StubSemanticMatcherService(SemanticMatchExplanation explanation) : ISemanticMatcherService
    {
        public Task<SemanticMatchExplanation> ExplainMatchAsync(
            string method,
            string path,
            IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> query,
            IReadOnlyDictionary<string, string> headers,
            string? body,
            IReadOnlyCollection<QueryMatchDefinition> candidates,
            Func<QueryMatchDefinition, bool>? candidateFilter = null,
            bool includeCandidateScores = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(explanation);
        }
    }

    // ---------------------------------------------------------------------------
    // GetConfigSnapshot — definitions directory
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetRuntimeMetrics_ReturnsZeroValues_BeforeRequestsAreRecorded()
    {
        var service = CreateService(EmptyDocument());

        var metrics = service.GetRuntimeMetrics();

        Assert.Equal(0, metrics.TotalRequestCount);
        Assert.Equal(0, metrics.MatchedRequestCount);
        Assert.Equal(0, metrics.UnmatchedRequestCount);
        Assert.Equal(0, metrics.FallbackResponseCount);
        Assert.Equal(0, metrics.SemanticMatchCount);
        Assert.Equal(0, metrics.AverageLatencyMilliseconds);
        Assert.Empty(metrics.StatusCodes);
        Assert.Empty(metrics.TopRoutes);
    }

    [Fact]
    public void RecordRequestMetrics_UpdatesMatchedCounts_StatusCodes_AndRoutes()
    {
        var service = CreateService(EmptyDocument());

        service.RecordRequestMetrics(
            CreateRecordedExplanation(matched: true, matchResult: "Matched", routeId: "listUsers", matchMode: "fallback"),
            StatusCodes.Status200OK,
            TimeSpan.FromMilliseconds(25));

        var metrics = service.GetRuntimeMetrics();

        Assert.Equal(1, metrics.TotalRequestCount);
        Assert.Equal(1, metrics.MatchedRequestCount);
        Assert.Equal(0, metrics.UnmatchedRequestCount);
        Assert.Equal(1, metrics.FallbackResponseCount);
        Assert.Equal(0, metrics.SemanticMatchCount);
        Assert.Equal(25, metrics.AverageLatencyMilliseconds);
        Assert.Collection(metrics.StatusCodes, entry =>
        {
            Assert.Equal(StatusCodes.Status200OK, entry.StatusCode);
            Assert.Equal(1, entry.RequestCount);
        });
        Assert.Collection(metrics.TopRoutes, entry =>
        {
            Assert.Equal("listUsers", entry.RouteId);
            Assert.Equal(1, entry.RequestCount);
        });
    }

    [Fact]
    public void RecordRequestMetrics_UpdatesUnmatchedAndSemanticCounts()
    {
        var service = CreateService(EmptyDocument());

        service.RecordRequestMetrics(
            CreateRecordedExplanation(matched: false, matchResult: "PathNotFound"),
            StatusCodes.Status404NotFound,
            TimeSpan.FromMilliseconds(10));
        service.RecordRequestMetrics(
            CreateRecordedExplanation(matched: true, matchResult: "Matched", routeId: "searchUsers", matchMode: "semantic"),
            StatusCodes.Status200OK,
            TimeSpan.FromMilliseconds(30));

        var metrics = service.GetRuntimeMetrics();

        Assert.Equal(2, metrics.TotalRequestCount);
        Assert.Equal(1, metrics.MatchedRequestCount);
        Assert.Equal(1, metrics.UnmatchedRequestCount);
        Assert.Equal(0, metrics.FallbackResponseCount);
        Assert.Equal(1, metrics.SemanticMatchCount);
        Assert.Equal(20, metrics.AverageLatencyMilliseconds);
    }

    [Fact]
    public void GetRuntimeMetrics_OrdersStatusCodesAndRoutes_ByUsage()
    {
        var service = CreateService(EmptyDocument());

        service.RecordRequestMetrics(
            CreateRecordedExplanation(matched: true, matchResult: "Matched", routeId: "b-route"),
            StatusCodes.Status200OK,
            TimeSpan.FromMilliseconds(10));
        service.RecordRequestMetrics(
            CreateRecordedExplanation(matched: true, matchResult: "Matched", routeId: "a-route"),
            StatusCodes.Status404NotFound,
            TimeSpan.FromMilliseconds(20));
        service.RecordRequestMetrics(
            CreateRecordedExplanation(matched: true, matchResult: "Matched", routeId: "a-route"),
            StatusCodes.Status404NotFound,
            TimeSpan.FromMilliseconds(30));

        var metrics = service.GetRuntimeMetrics();

        Assert.Collection(
            metrics.StatusCodes,
            first =>
            {
                Assert.Equal(StatusCodes.Status404NotFound, first.StatusCode);
                Assert.Equal(2, first.RequestCount);
            },
            second =>
            {
                Assert.Equal(StatusCodes.Status200OK, second.StatusCode);
                Assert.Equal(1, second.RequestCount);
            });
        Assert.Collection(
            metrics.TopRoutes,
            first =>
            {
                Assert.Equal("a-route", first.RouteId);
                Assert.Equal(2, first.RequestCount);
            },
            second =>
            {
                Assert.Equal("b-route", second.RouteId);
                Assert.Equal(1, second.RequestCount);
            });
    }

    [Fact]
    public void StubInspectionRuntimeStore_RecordRequestMetrics_ClampsNegativeLatency()
    {
        var store = new StubInspectionRuntimeStore();

        store.RecordRequestMetrics(
            CreateRecordedExplanation(matched: true, matchResult: "Matched", routeId: "listUsers"),
            StatusCodes.Status200OK,
            TimeSpan.FromMilliseconds(-10));

        var metrics = store.GetRuntimeMetrics();

        Assert.Equal(0, metrics.AverageLatencyMilliseconds);
        Assert.Collection(metrics.TopRoutes, route => Assert.Equal("listUsers", route.RouteId));
    }

    [Fact]
    public void ResetRuntimeMetrics_ClearsMetricsAndRecentRequests_ButKeepsLastMatchExplanation()
    {
        var service = CreateService(EmptyDocument());
        var explanation = CreateRecordedExplanation(matched: true, matchResult: "Matched", routeId: "listUsers", matchMode: "fallback");

        service.RecordRequestMetrics(explanation, StatusCodes.Status200OK, TimeSpan.FromMilliseconds(25));
        service.RecordRecentRequest(
            DateTimeOffset.Parse("2026-04-07T00:00:00Z"),
            HttpMethods.Get,
            "/users",
            explanation,
            StatusCodes.Status200OK,
            TimeSpan.FromMilliseconds(25));
        service.RecordLastMatchExplanation(explanation);

        service.ResetRuntimeMetrics();

        var metrics = service.GetRuntimeMetrics();
        Assert.Equal(0, metrics.TotalRequestCount);
        Assert.Equal(0, metrics.MatchedRequestCount);
        Assert.Equal(0, metrics.UnmatchedRequestCount);
        Assert.Equal(0, metrics.FallbackResponseCount);
        Assert.Equal(0, metrics.SemanticMatchCount);
        Assert.Equal(0, metrics.AverageLatencyMilliseconds);
        Assert.Empty(metrics.StatusCodes);
        Assert.Empty(metrics.TopRoutes);
        Assert.Empty(service.GetRecentRequests(20));
        Assert.Same(explanation, service.GetLastMatchExplanation());
    }

    [Fact]
    public void GetRecentRequests_ReturnsEmpty_WhenNothingHasBeenRecorded()
    {
        var service = CreateService(EmptyDocument());

        var requests = service.GetRecentRequests(20);

        Assert.Empty(requests);
    }

    [Fact]
    public void RecordRecentRequest_ReturnsNewestFirst_AndCapturesFailureReason()
    {
        var service = CreateService(EmptyDocument());

        service.RecordRecentRequest(
            DateTimeOffset.Parse("2026-04-07T00:00:00Z"),
            HttpMethods.Get,
            "/users",
            new MatchExplanationInfo
            {
                SelectionReason = "No configured route matched the supplied request path.",
                Result = new MatchSimulationInfo
                {
                    Matched = false,
                    MatchResult = "PathNotFound",
                }
            },
            StatusCodes.Status404NotFound,
            TimeSpan.FromMilliseconds(15));
        service.RecordRecentRequest(
            DateTimeOffset.Parse("2026-04-07T00:00:01Z"),
            HttpMethods.Post,
            "/orders",
            CreateRecordedExplanation(matched: true, matchResult: "Matched", routeId: "createOrder", matchMode: "fallback"),
            StatusCodes.Status201Created,
            TimeSpan.FromMilliseconds(25));

        var requests = service.GetRecentRequests(20);

        Assert.Equal(2, requests.Count);
        Assert.Equal("/orders", requests[0].Path);
        Assert.Equal("createOrder", requests[0].RouteId);
        Assert.Equal("fallback", requests[0].MatchMode);
        Assert.Null(requests[0].FailureReason);
        Assert.Equal("/users", requests[1].Path);
        Assert.Equal("No configured route matched the supplied request path.", requests[1].FailureReason);
    }

    [Fact]
    public void GetRecentRequests_RespectsLimit_AndRetentionBound()
    {
        var service = CreateService(EmptyDocument());

        for (var index = 0; index < 105; index++)
        {
            service.RecordRecentRequest(
                DateTimeOffset.UtcNow.AddSeconds(index),
                HttpMethods.Get,
                $"/requests/{index}",
                CreateRecordedExplanation(matched: true, matchResult: "Matched", routeId: $"route-{index}"),
                StatusCodes.Status200OK,
                TimeSpan.FromMilliseconds(index));
        }

        var limited = service.GetRecentRequests(3);
        var all = service.GetRecentRequests(200);

        Assert.Equal(3, limited.Count);
        Assert.Equal("/requests/104", limited[0].Path);
        Assert.Equal("/requests/102", limited[2].Path);
        Assert.Equal(100, all.Count);
        Assert.Equal("/requests/104", all[0].Path);
        Assert.Equal("/requests/5", all[^1].Path);
    }

    [Fact]
    public void StubInspectionRuntimeStore_GetRecentRequests_ReturnsEmpty_WhenLimitIsZeroOrNegative()
    {
        var store = new StubInspectionRuntimeStore();

        store.RecordRecentRequest(
            DateTimeOffset.Parse("2026-04-07T00:00:00Z"),
            HttpMethods.Get,
            "/users",
            CreateRecordedExplanation(matched: true, matchResult: "Matched", routeId: "listUsers"),
            StatusCodes.Status200OK,
            TimeSpan.FromMilliseconds(15));

        Assert.Empty(store.GetRecentRequests(0));
        Assert.Empty(store.GetRecentRequests(-1));
    }

    [Fact]
    public void StubInspectionRuntimeStore_GetLastMatchExplanation_ReturnsRecordedInstance()
    {
        var store = new StubInspectionRuntimeStore();
        var explanation = CreateRecordedExplanation(matched: true, matchResult: "Matched", routeId: "listUsers");

        store.RecordLastMatchExplanation(explanation);

        Assert.Same(explanation, store.GetLastMatchExplanation());
    }
}
