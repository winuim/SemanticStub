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

public sealed class StubInspectionScenarioTests
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

    // ---------------------------------------------------------------------------
    // Scenario state inspection
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetScenarioStates_ReturnsConfiguredScenarios_WithInitialStateByDefault()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/checkout"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["409"] = new()
                            {
                                Scenario = new ScenarioDefinition
                                {
                                    Name = "checkout-flow",
                                    State = "initial",
                                    Next = "confirmed"
                                }
                            }
                        }
                    }
                }
            }
        };

        var scenario = Assert.Single(CreateService(document).GetScenarioStates());

        Assert.Equal("checkout-flow", scenario.Name);
        Assert.Equal("initial", scenario.CurrentState);
        Assert.Null(scenario.LastUpdatedTimestamp);
    }

    [Fact]
    public void GetScenarioStates_ReflectsAdvancedScenarioStateAndTimestamp()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/checkout"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["409"] = new()
                            {
                                Scenario = new ScenarioDefinition
                                {
                                    Name = "checkout-flow",
                                    State = "initial",
                                    Next = "confirmed"
                                }
                            }
                        }
                    }
                }
            }
        };
        var loader = new TestStubDefinitionLoader(document);
        var scenarioService = new ScenarioService();
        var state = new StubDefinitionState(loader, scenarioService, NullLogger<StubDefinitionState>.Instance);
        var settings = Options.Create(new StubSettings());
        var stubService = CreateStubService(state, scenarioService, CreateMatcherService(), new NoOpSemanticMatcherService());
        var service = new StubInspectionService(
            state,
            loader,
            settings,
            stubService,
            new StubInspectionRuntimeStore(),
            new StubInspectionScenarioCoordinator(state, scenarioService));

        scenarioService.Advance(new ScenarioDefinition
        {
            Name = "checkout-flow",
            State = "initial",
            Next = "confirmed"
        });

        var scenario = Assert.Single(service.GetScenarioStates());

        Assert.Equal("confirmed", scenario.CurrentState);
        Assert.NotNull(scenario.LastUpdatedTimestamp);
    }

    [Fact]
    public void ResetScenarioState_ReturnsFalse_WhenScenarioDoesNotExist()
    {
        var service = CreateService(EmptyDocument());

        var reset = service.ResetScenarioState("missing");

        Assert.False(reset);
    }

    [Fact]
    public void ResetScenarioStates_ResetsAllConfiguredScenariosWithTimestamp()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/checkout"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["409"] = new()
                            {
                                Scenario = new ScenarioDefinition
                                {
                                    Name = "checkout-flow",
                                    State = "initial",
                                    Next = "confirmed"
                                }
                            }
                        },
                        Matches =
                        [
                            new QueryMatchDefinition
                                {
                                    Response = new QueryMatchResponseDefinition
                                    {
                                        StatusCode = 200,
                                        Scenario = new ScenarioDefinition
                                        {
                                            Name = "payment-flow",
                                            State = "initial",
                                            Next = "authorized"
                                        }
                                    }
                                }
                        ]
                    }
                }
            }
        };
        var loader = new TestStubDefinitionLoader(document);
        var scenarioService = new ScenarioService();
        var state = new StubDefinitionState(loader, scenarioService, NullLogger<StubDefinitionState>.Instance);
        var settings = Options.Create(new StubSettings());
        var stubService = CreateStubService(state, scenarioService, CreateMatcherService(), new NoOpSemanticMatcherService());
        var service = new StubInspectionService(
            state,
            loader,
            settings,
            stubService,
            new StubInspectionRuntimeStore(),
            new StubInspectionScenarioCoordinator(state, scenarioService));

        scenarioService.Advance(new ScenarioDefinition { Name = "checkout-flow", State = "initial", Next = "confirmed" });
        scenarioService.Advance(new ScenarioDefinition { Name = "payment-flow", State = "initial", Next = "authorized" });

        service.ResetScenarioStates();

        var scenarios = service.GetScenarioStates();

        Assert.Equal(2, scenarios.Count);
        Assert.All(scenarios, scenario =>
        {
            Assert.Equal("initial", scenario.CurrentState);
            Assert.NotNull(scenario.LastUpdatedTimestamp);
        });
    }

    [Fact]
    public void StubInspectionScenarioCoordinator_ResetScenarioState_ReturnsFalse_WhenScenarioDoesNotExist()
    {
        var loader = new TestStubDefinitionLoader(EmptyDocument());
        var scenarioService = new ScenarioService();
        var state = new StubDefinitionState(loader, scenarioService, NullLogger<StubDefinitionState>.Instance);
        var coordinator = new StubInspectionScenarioCoordinator(state, scenarioService);

        var reset = coordinator.ResetScenarioState("missing");

        Assert.False(reset);
    }

    [Fact]
    public void StubInspectionScenarioCoordinator_GetScenarioStates_ReflectsAdvancedScenarioState()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/checkout"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["409"] = new()
                            {
                                Scenario = new ScenarioDefinition
                                {
                                    Name = "checkout-flow",
                                    State = "initial",
                                    Next = "confirmed",
                                },
                            },
                        },
                    },
                },
            },
        };
        var loader = new TestStubDefinitionLoader(document);
        var scenarioService = new ScenarioService();
        var state = new StubDefinitionState(loader, scenarioService, NullLogger<StubDefinitionState>.Instance);
        var coordinator = new StubInspectionScenarioCoordinator(state, scenarioService);

        scenarioService.Advance(new ScenarioDefinition
        {
            Name = "checkout-flow",
            State = "initial",
            Next = "confirmed",
        });

        var scenario = Assert.Single(coordinator.GetScenarioStates());

        Assert.Equal("checkout-flow", scenario.Name);
        Assert.Equal("confirmed", scenario.CurrentState);
        Assert.NotNull(scenario.LastUpdatedTimestamp);
    }
}
