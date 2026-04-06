using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class StubInspectionServiceTests
{
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
        IMatcherService? matcherService = null,
        ISemanticMatcherService? semanticMatcherService = null)
    {
        var loader = new TestStubDefinitionLoader(document, directoryPath);
        var scenarioService = new ScenarioService();
        var state = new StubDefinitionState(loader, scenarioService, NullLogger<StubDefinitionState>.Instance);
        var settings = Options.Create(new StubSettings
        {
            SemanticMatching = new SemanticMatchingSettings { Enabled = semanticMatchingEnabled },
        });
        return new StubInspectionService(
            state,
            loader,
            settings,
            scenarioService,
            matcherService ?? new MatcherService(),
            semanticMatcherService ?? new NoOpSemanticMatcherService());
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

    private sealed class NoOpSemanticMatcherService : ISemanticMatcherService
    {
        public Task<QueryMatchDefinition?> FindBestMatchAsync(
            string method,
            string path,
            IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> query,
            IReadOnlyDictionary<string, string> headers,
            string? body,
            IReadOnlyCollection<QueryMatchDefinition> candidates,
            Func<QueryMatchDefinition, bool>? candidateFilter = null)
        {
            return Task.FromResult<QueryMatchDefinition?>(null);
        }

        public Task<SemanticMatchExplanation> ExplainMatchAsync(
            string method,
            string path,
            IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> query,
            IReadOnlyDictionary<string, string> headers,
            string? body,
            IReadOnlyCollection<QueryMatchDefinition> candidates,
            Func<QueryMatchDefinition, bool>? candidateFilter = null,
            bool includeCandidateScores = false)
        {
            return Task.FromResult(new SemanticMatchExplanation());
        }
    }

    private sealed class StubSemanticMatcherService(SemanticMatchExplanation explanation) : ISemanticMatcherService
    {
        public Task<QueryMatchDefinition?> FindBestMatchAsync(
            string method,
            string path,
            IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> query,
            IReadOnlyDictionary<string, string> headers,
            string? body,
            IReadOnlyCollection<QueryMatchDefinition> candidates,
            Func<QueryMatchDefinition, bool>? candidateFilter = null)
        {
            return Task.FromResult(explanation.SelectedCandidate);
        }

        public Task<SemanticMatchExplanation> ExplainMatchAsync(
            string method,
            string path,
            IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> query,
            IReadOnlyDictionary<string, string> headers,
            string? body,
            IReadOnlyCollection<QueryMatchDefinition> candidates,
            Func<QueryMatchDefinition, bool>? candidateFilter = null,
            bool includeCandidateScores = false)
        {
            return Task.FromResult(explanation);
        }
    }

    // ---------------------------------------------------------------------------
    // GetConfigSnapshot — definitions directory
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetConfigSnapshot_ReturnsDefinitionsDirectoryPath()
    {
        var service = CreateService(EmptyDocument(), directoryPath: "/custom/stubs");

        var snapshot = service.GetConfigSnapshot();

        Assert.Equal("/custom/stubs", snapshot.DefinitionsDirectoryPath);
    }

    // ---------------------------------------------------------------------------
    // GetConfigSnapshot — route count
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetConfigSnapshot_RouteCount_IsZeroForEmptyDocument()
    {
        var service = CreateService(EmptyDocument());

        var snapshot = service.GetConfigSnapshot();

        Assert.Equal(0, snapshot.RouteCount);
    }

    [Fact]
    public void GetConfigSnapshot_RouteCount_CountsEachMethodSeparately()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/orders"] = new()
                {
                    Get = new OperationDefinition { Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal) { ["200"] = new() } },
                    Post = new OperationDefinition { Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal) { ["201"] = new() } },
                },
                ["/users"] = new()
                {
                    Get = new OperationDefinition { Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal) { ["200"] = new() } },
                },
            },
        };

        var service = CreateService(document);

        Assert.Equal(3, service.GetConfigSnapshot().RouteCount);
    }

    // ---------------------------------------------------------------------------
    // GetConfigSnapshot — semantic matching flag
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetConfigSnapshot_SemanticMatchingEnabled_WhenSettingIsTrue()
    {
        var service = CreateService(EmptyDocument(), semanticMatchingEnabled: true);

        Assert.True(service.GetConfigSnapshot().SemanticMatchingEnabled);
    }

    [Fact]
    public void GetConfigSnapshot_SemanticMatchingDisabled_WhenSettingIsFalse()
    {
        var service = CreateService(EmptyDocument(), semanticMatchingEnabled: false);

        Assert.False(service.GetConfigSnapshot().SemanticMatchingEnabled);
    }

    // ---------------------------------------------------------------------------
    // GetConfigSnapshot — snapshot timestamp
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetConfigSnapshot_SnapshotTimestamp_IsRecentUtc()
    {
        var before = DateTimeOffset.UtcNow;
        var service = CreateService(EmptyDocument());

        var snapshot = service.GetConfigSnapshot();
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(TimeSpan.Zero, snapshot.SnapshotTimestamp.Offset);
        Assert.True(snapshot.SnapshotTimestamp >= before);
        Assert.True(snapshot.SnapshotTimestamp <= after);
    }

    // ---------------------------------------------------------------------------
    // GetConfigSnapshot — configuration hash stability
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetConfigSnapshot_ConfigurationHash_IsStableForSameDocument()
    {
        var document = SingleGetDocument();
        var service = CreateService(document);

        var hash1 = service.GetConfigSnapshot().ConfigurationHash;
        var hash2 = service.GetConfigSnapshot().ConfigurationHash;

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GetConfigSnapshot_ConfigurationHash_DiffersForDifferentDocuments()
    {
        var hash1 = CreateService(SingleGetDocument("/a")).GetConfigSnapshot().ConfigurationHash;
        var hash2 = CreateService(SingleGetDocument("/b")).GetConfigSnapshot().ConfigurationHash;

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GetConfigSnapshot_ConfigurationHash_DiffersWhenOperationIdChanges()
    {
        var hash1 = CreateService(SingleGetDocument("/hello", operationId: "opA")).GetConfigSnapshot().ConfigurationHash;
        var hash2 = CreateService(SingleGetDocument("/hello", operationId: "opB")).GetConfigSnapshot().ConfigurationHash;

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GetConfigSnapshot_ConfigurationHash_DiffersWhenResponseAdded()
    {
        var docWith200 = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/items"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new(),
                        },
                    },
                },
            },
        };

        var docWith200And404 = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/items"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new(),
                            ["404"] = new(),
                        },
                    },
                },
            },
        };

        var hash1 = CreateService(docWith200).GetConfigSnapshot().ConfigurationHash;
        var hash2 = CreateService(docWith200And404).GetConfigSnapshot().ConfigurationHash;

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GetConfigSnapshot_ConfigurationHash_DiffersWhenSemanticMatchAdded()
    {
        var docWithout = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/search"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal) { ["200"] = new() },
                    },
                },
            },
        };

        var docWith = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/search"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Matches = [new QueryMatchDefinition { SemanticMatch = "find admin" }],
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal) { ["200"] = new() },
                    },
                },
            },
        };

        var hash1 = CreateService(docWithout).GetConfigSnapshot().ConfigurationHash;
        var hash2 = CreateService(docWith).GetConfigSnapshot().ConfigurationHash;

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GetConfigSnapshot_ConfigurationHash_IsLowercaseHex()
    {
        var service = CreateService(SingleGetDocument());

        var hash = service.GetConfigSnapshot().ConfigurationHash;

        Assert.Matches("^[0-9a-f]+$", hash);
    }

    // ---------------------------------------------------------------------------
    // GetRoutes — empty document
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetRoutes_EmptyDocument_ReturnsEmptyList()
    {
        var service = CreateService(EmptyDocument());

        Assert.Empty(service.GetRoutes());
    }

    // ---------------------------------------------------------------------------
    // GetRoutes — RouteId
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetRoutes_UsesOperationId_WhenPresent()
    {
        var service = CreateService(SingleGetDocument("/hello", operationId: "sayHello"));

        var route = Assert.Single(service.GetRoutes());
        Assert.Equal("sayHello", route.RouteId);
    }

    [Fact]
    public void GetRoutes_UsesMethodColonPath_WhenOperationIdIsEmpty()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/users"] = new()
                {
                    Get = new OperationDefinition
                    {
                        OperationId = string.Empty,
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal) { ["200"] = new() },
                    },
                },
            },
        };

        var service = CreateService(document);

        var route = Assert.Single(service.GetRoutes());
        Assert.Equal("GET:/users", route.RouteId);
    }

    // ---------------------------------------------------------------------------
    // GetRoutes — Method and PathPattern
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetRoutes_Method_IsUpperCase()
    {
        var service = CreateService(SingleGetDocument("/hello"));

        var route = Assert.Single(service.GetRoutes());
        Assert.Equal("GET", route.Method);
    }

    [Fact]
    public void GetRoutes_PathPattern_MatchesYamlPath()
    {
        var service = CreateService(SingleGetDocument("/orders/{id}"));

        var route = Assert.Single(service.GetRoutes());
        Assert.Equal("/orders/{id}", route.PathPattern);
    }

    [Fact]
    public void GetRoutes_OneEntryPerMethodPerPath()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/items"] = new()
                {
                    Get = new OperationDefinition { Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal) { ["200"] = new() } },
                    Post = new OperationDefinition { Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal) { ["201"] = new() } },
                },
            },
        };

        var routes = CreateService(document).GetRoutes();

        Assert.Equal(2, routes.Count);
        Assert.Contains(routes, r => r.Method == "GET");
        Assert.Contains(routes, r => r.Method == "POST");
    }

    // ---------------------------------------------------------------------------
    // GetRoutes — UsesSemanticMatching
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetRoutes_UsesSemanticMatching_TrueWhenAnyMatchHasSemanticMatch()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/search"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Matches =
                        [
                            new QueryMatchDefinition { SemanticMatch = "find an admin user" },
                        ],
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal) { ["200"] = new() },
                    },
                },
            },
        };

        var route = Assert.Single(CreateService(document).GetRoutes());
        Assert.True(route.UsesSemanticMatching);
    }

    [Fact]
    public void GetRoutes_UsesSemanticMatching_FalseWhenNoMatchHasSemanticMatch()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/search"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Matches =
                        [
                            new QueryMatchDefinition { Query = new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "admin" } },
                        ],
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal) { ["200"] = new() },
                    },
                },
            },
        };

        var route = Assert.Single(CreateService(document).GetRoutes());
        Assert.False(route.UsesSemanticMatching);
    }

    // ---------------------------------------------------------------------------
    // GetRoutes — UsesScenario
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetRoutes_UsesScenario_TrueWhenTopLevelResponseHasScenario()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/flow"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new()
                            {
                                Scenario = new ScenarioDefinition { Name = "checkout", State = "idle", Next = "pending" },
                            },
                        },
                    },
                },
            },
        };

        var route = Assert.Single(CreateService(document).GetRoutes());
        Assert.True(route.UsesScenario);
    }

    [Fact]
    public void GetRoutes_UsesScenario_TrueWhenConditionalMatchResponseHasScenario()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/flow"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Matches =
                        [
                            new QueryMatchDefinition
                            {
                                Response = new QueryMatchResponseDefinition
                                {
                                    StatusCode = 200,
                                    Scenario = new ScenarioDefinition { Name = "checkout", State = "idle", Next = "pending" },
                                },
                            },
                        ],
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal) { ["200"] = new() },
                    },
                },
            },
        };

        var route = Assert.Single(CreateService(document).GetRoutes());
        Assert.True(route.UsesScenario);
    }

    [Fact]
    public void GetRoutes_UsesScenario_FalseWhenNoResponseHasScenario()
    {
        var service = CreateService(SingleGetDocument());

        var route = Assert.Single(service.GetRoutes());
        Assert.False(route.UsesScenario);
    }

    // ---------------------------------------------------------------------------
    // GetRoutes — ResponseCount
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetRoutes_ResponseCount_MatchesResponsesDictionaryCount()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/items"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new(),
                            ["404"] = new(),
                            ["500"] = new(),
                        },
                    },
                },
            },
        };

        var route = Assert.Single(CreateService(document).GetRoutes());
        Assert.Equal(3, route.ResponseCount);
    }

    [Fact]
    public void GetRoutes_ResponseCount_IsZeroWhenNoResponsesDefined()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/ping"] = new()
                {
                    Get = new OperationDefinition(),
                },
            },
        };

        var route = Assert.Single(CreateService(document).GetRoutes());
        Assert.Equal(0, route.ResponseCount);
    }

    // ---------------------------------------------------------------------------
    // GetScenarioStates / ResetScenarioStates
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
        var service = new StubInspectionService(state, loader, settings, scenarioService, new MatcherService(), new NoOpSemanticMatcherService());

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
        var service = new StubInspectionService(state, loader, settings, scenarioService, new MatcherService(), new NoOpSemanticMatcherService());

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
    public void GetLastMatchExplanation_ReturnsNull_WhenNothingHasBeenRecorded()
    {
        var service = CreateService(EmptyDocument());

        var explanation = service.GetLastMatchExplanation();

        Assert.Null(explanation);
    }

    [Fact]
    public async Task TestMatchAsync_ReturnsDeterministicMatchWithoutMutatingScenarioState()
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
                            ["200"] = new()
                            {
                                Scenario = new ScenarioDefinition
                                {
                                    Name = "checkout-flow",
                                    State = "initial",
                                    Next = "confirmed"
                                },
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new() { Example = new Dictionary<object, object> { ["ok"] = true } }
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
        var service = new StubInspectionService(
            state,
            loader,
            Options.Create(new StubSettings()),
            scenarioService,
            new MatcherService(),
            new NoOpSemanticMatcherService());

        var result = await service.TestMatchAsync(new MatchRequestInfo
        {
            Method = "POST",
            Path = "/checkout"
        });

        Assert.True(result.Matched);
        Assert.Equal("Matched", result.MatchResult);
        Assert.Equal("fallback", result.MatchMode);
        Assert.Equal("initial", scenarioService.GetSnapshot("checkout-flow").State);
        Assert.Null(scenarioService.GetSnapshot("checkout-flow").LastUpdatedTimestamp);
    }

    [Fact]
    public async Task ExplainMatchAsync_ReturnsDeterministicCandidateEvaluations()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/users"] = new()
                {
                    Get = new OperationDefinition
                    {
                        OperationId = "listUsers",
                        Matches =
                        [
                            new QueryMatchDefinition
                            {
                                Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                                {
                                    ["role"] = "admin"
                                },
                                Response = new QueryMatchResponseDefinition
                                {
                                    StatusCode = 200,
                                    Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                    {
                                        ["application/json"] = new() { Example = new Dictionary<object, object> { ["role"] = "admin" } }
                                    }
                                }
                            },
                            new QueryMatchDefinition
                            {
                                Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                                {
                                    ["role"] = "guest"
                                },
                                Response = new QueryMatchResponseDefinition
                                {
                                    StatusCode = 200,
                                    Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                    {
                                        ["application/json"] = new() { Example = new Dictionary<object, object> { ["role"] = "guest" } }
                                    }
                                }
                            }
                        ],
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new()
                            {
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new() { Example = new Dictionary<object, object> { ["role"] = "default" } }
                                }
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService(document);

        var explanation = await service.ExplainMatchAsync(new MatchRequestInfo
        {
            Method = "GET",
            Path = "/users",
            Query = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["role"] = ["admin"]
            },
            IncludeCandidates = true
        });

        Assert.True(explanation.Result.Matched);
        Assert.Equal("exact", explanation.Result.MatchMode);
        Assert.Equal("listUsers", explanation.Result.RouteId);
        Assert.Equal(2, explanation.DeterministicCandidates.Count);
        Assert.True(explanation.DeterministicCandidates[0].Matched);
        Assert.False(explanation.DeterministicCandidates[1].Matched);
        Assert.Equal(2, explanation.Result.Candidates.Count);
    }

    [Fact]
    public async Task ExplainMatchAsync_ReturnsSemanticEvaluationDetailsWhenRequested()
    {
        var semanticCandidate = new QueryMatchDefinition
        {
            SemanticMatch = "find administrator user accounts",
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 200,
                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                {
                    ["application/json"] = new() { Example = new Dictionary<object, object> { ["result"] = "admin-user" } }
                }
            }
        };
        var otherCandidate = new QueryMatchDefinition
        {
            SemanticMatch = "show unpaid invoices",
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 200,
                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                {
                    ["application/json"] = new() { Example = new Dictionary<object, object> { ["result"] = "invoices" } }
                }
            }
        };
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/semantic-search"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Matches = [semanticCandidate, otherCandidate],
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["404"] = new()
                            {
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new() { Example = new Dictionary<object, object> { ["message"] = "no match" } }
                                }
                            }
                        }
                    }
                }
            }
        };
        var semanticMatcher = new StubSemanticMatcherService(new SemanticMatchExplanation
        {
            Attempted = true,
            SelectedCandidate = semanticCandidate,
            SelectedScore = 0.97d,
            Threshold = 0.8d,
            RequiredMargin = 0.05d,
            SecondBestScore = 0.81d,
            MarginToSecondBest = 0.16d,
            CandidateScores =
            [
                new SemanticCandidateScore
                {
                    Candidate = semanticCandidate,
                    Eligible = true,
                    Score = 0.97d,
                    AboveThreshold = true
                },
                new SemanticCandidateScore
                {
                    Candidate = otherCandidate,
                    Eligible = true,
                    Score = 0.81d,
                    AboveThreshold = true
                }
            ]
        });
        var service = CreateService(document, semanticMatchingEnabled: true, semanticMatcherService: semanticMatcher);

        var explanation = await service.ExplainMatchAsync(new MatchRequestInfo
        {
            Method = "POST",
            Path = "/semantic-search",
            Body = "find admin users by email",
            IncludeSemanticCandidates = true
        });

        Assert.True(explanation.Result.Matched);
        Assert.Equal("semantic", explanation.Result.MatchMode);
        Assert.NotNull(explanation.SemanticEvaluation);
        Assert.Equal(0.97d, explanation.SemanticEvaluation!.SelectedScore);
        Assert.Equal(0.8d, explanation.SemanticEvaluation.Threshold);
        Assert.Equal(2, explanation.SemanticEvaluation.Candidates.Count);
    }

    [Fact]
    public async Task RecordLastMatchExplanation_StoresExplanationForMostRecentRealRequest()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/users"] = new()
                {
                    Get = new OperationDefinition
                    {
                        OperationId = "listUsers",
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new()
                            {
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new() { Example = new Dictionary<object, object> { ["users"] = Array.Empty<object>() } }
                                }
                            }
                        }
                    }
                }
            }
        };
        var service = CreateService(document);

        var evaluated = await service.ExplainMatchAsync(new MatchRequestInfo
        {
            Method = "GET",
            Path = "/users"
        });
        service.RecordLastMatchExplanation(evaluated);

        var explanation = service.GetLastMatchExplanation();

        Assert.NotNull(explanation);
        Assert.True(explanation!.Result.Matched);
        Assert.Equal("fallback", explanation.Result.MatchMode);
        Assert.Equal("listUsers", explanation.Result.RouteId);
    }
}
