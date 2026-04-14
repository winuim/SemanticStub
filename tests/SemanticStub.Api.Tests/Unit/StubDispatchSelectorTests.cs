using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class StubDispatchSelectorTests
{
    private static MatcherService CreateMatcherService()
    {
        return new MatcherService(new JsonBodyMatcher(), new FormBodyMatcher(), new QueryValueMatcher(), new RegexQueryMatcher());
    }

    [Fact]
    public async Task SelectAsync_ReturnsResponseNotConfiguredWhenDeterministicCandidateHasInvalidResponse()
    {
        var matcherService = CreateMatcherService();
        var scenarioService = new ScenarioService();
        var responseBuilder = new StubResponseBuilder(_ => throw new InvalidOperationException("No file loading expected."));
        var selector = new StubDispatchSelector(
            matcherService,
            semanticMatcherService: null,
            responseBuilder,
            new StubDefaultResponseSelector(responseBuilder, scenarioService),
            scenarioService,
            logger: null);
        var candidate = new QueryMatchDefinition
        {
            Query = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = "admin"
            },
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 200
            }
        };
        var pathItem = new PathItemDefinition();
        var operation = new OperationDefinition
        {
            Matches = [candidate]
        };
        var query = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["role"] = new("admin")
        };

        var result = await selector.SelectAsync(HttpMethods.Get, "/users", pathItem, operation, query, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), null, mutateScenarioState: true, includeSemanticCandidates: false);

        Assert.Equal(StubMatchResult.ResponseNotConfigured, result.Result);
        Assert.Equal("exact", result.MatchMode);
        Assert.Same(candidate, result.SelectedCandidate);
        Assert.Equal("200", result.SelectedResponseId);
        Assert.Equal("Deterministic candidate 0 matched, but its response is not configured.", result.SelectionReason);
    }

    [Fact]
    public async Task SelectAsync_IncludesCandidateIndexInDeterministicSelectionReason()
    {
        var matcherService = CreateMatcherService();
        var scenarioService = new ScenarioService();
        var responseBuilder = new StubResponseBuilder(_ => throw new InvalidOperationException("No file loading expected."));
        var selector = new StubDispatchSelector(
            matcherService,
            semanticMatcherService: null,
            responseBuilder,
            new StubDefaultResponseSelector(responseBuilder, scenarioService),
            scenarioService,
            logger: null);
        var ignoredCandidate = new QueryMatchDefinition
        {
            Query = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = "user"
            },
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 201,
                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                {
                    ["application/json"] = new()
                    {
                        Example = new Dictionary<object, object> { ["message"] = "user" }
                    }
                }
            }
        };
        var selectedCandidate = new QueryMatchDefinition
        {
            Query = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = "admin",
                ["team"] = "ops"
            },
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 202,
                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                {
                    ["application/json"] = new()
                    {
                        Example = new Dictionary<object, object> { ["message"] = "admin" }
                    }
                }
            }
        };
        var operation = new OperationDefinition
        {
            Matches = [ignoredCandidate, selectedCandidate]
        };
        var query = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["role"] = new("admin"),
            ["team"] = new("ops")
        };

        var result = await selector.SelectAsync(
            HttpMethods.Get,
            "/users",
            new PathItemDefinition(),
            operation,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            null,
            mutateScenarioState: true,
            includeSemanticCandidates: false);

        Assert.Equal(StubMatchResult.Matched, result.Result);
        Assert.Equal("Deterministic candidate 1 matched all configured conditions and was selected.", result.SelectionReason);
    }

    [Fact]
    public async Task SelectAsync_UsesSemanticFallbackWhenDeterministicMatchFails()
    {
        var scenarioService = new ScenarioService();
        var responseBuilder = new StubResponseBuilder(_ => throw new InvalidOperationException("No file loading expected."));
        var semanticCandidate = new QueryMatchDefinition
        {
            SemanticMatch = "find admin users",
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 202,
                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                {
                    ["application/json"] = new()
                    {
                        Example = new Dictionary<object, object> { ["message"] = "semantic-admin" }
                    }
                }
            }
        };
        var selector = new StubDispatchSelector(
            CreateMatcherService(),
            new StubSemanticMatcherService(new SemanticMatchExplanation
            {
                Attempted = true,
                SelectedCandidate = semanticCandidate
            }),
            responseBuilder,
            new StubDefaultResponseSelector(responseBuilder, scenarioService),
            scenarioService,
            logger: null);

        var result = await selector.SelectAsync(
            HttpMethods.Get,
            "/users",
            new PathItemDefinition(),
            new OperationDefinition { Matches = [semanticCandidate] },
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            null,
            mutateScenarioState: true,
            includeSemanticCandidates: false);

        Assert.Equal(StubMatchResult.Matched, result.Result);
        Assert.Equal("semantic", result.MatchMode);
        Assert.Same(semanticCandidate, result.SelectedCandidate);
        Assert.NotNull(result.Response);
        Assert.Equal(202, result.Response!.StatusCode);
    }

    [Fact]
    public async Task SelectAsync_AdvancesDefaultScenarioOnlyWhenMutatingState()
    {
        var scenarioService = new ScenarioService();
        var responseBuilder = new StubResponseBuilder(_ => throw new InvalidOperationException("No file loading expected."));
        var selector = new StubDispatchSelector(
            CreateMatcherService(),
            semanticMatcherService: null,
            responseBuilder,
            new StubDefaultResponseSelector(responseBuilder, scenarioService),
            scenarioService,
            logger: null);
        var operation = new OperationDefinition
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
                    },
                    Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                    {
                        ["application/json"] = new()
                        {
                            Example = new Dictionary<object, object> { ["result"] = "pending" }
                        }
                    }
                }
            }
        };

        var dryRun = await selector.SelectAsync(
            HttpMethods.Post,
            "/checkout",
            new PathItemDefinition(),
            operation,
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            null,
            mutateScenarioState: false,
            includeSemanticCandidates: false);
        var snapshotAfterDryRun = scenarioService.GetSnapshot("checkout-flow");
        var liveRun = await selector.SelectAsync(
            HttpMethods.Post,
            "/checkout",
            new PathItemDefinition(),
            operation,
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            null,
            mutateScenarioState: true,
            includeSemanticCandidates: false);

        Assert.Equal(StubMatchResult.Matched, dryRun.Result);
        Assert.Equal("initial", snapshotAfterDryRun.State);
        Assert.Equal(StubMatchResult.Matched, liveRun.Result);
        Assert.Equal("confirmed", scenarioService.GetSnapshot("checkout-flow").State);
    }

    private sealed class StubSemanticMatcherService(SemanticMatchExplanation explanation) : ISemanticMatcherService
    {
        public Task<QueryMatchDefinition?> FindBestMatchAsync(
            string method,
            string path,
            IReadOnlyDictionary<string, StringValues> query,
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
            IReadOnlyDictionary<string, StringValues> query,
            IReadOnlyDictionary<string, string> headers,
            string? body,
            IReadOnlyCollection<QueryMatchDefinition> candidates,
            Func<QueryMatchDefinition, bool>? candidateFilter = null,
            bool includeCandidateScores = false)
        {
            return Task.FromResult(explanation);
        }
    }
}
