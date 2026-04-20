using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
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

namespace SemanticStub.Api.Tests.Unit.Resolution;

public sealed class StubServiceScenarioTests
{
    private static readonly Func<string, string> ThrowingResponseFileReader =
        _ => throw new InvalidOperationException("No response file reader configured.");

    private static MatcherService CreateMatcherService()
    {
        return new MatcherService(new JsonBodyMatcher(), new FormBodyMatcher(), new QueryValueMatcher(), new RegexQueryMatcher());
    }

    private static StubResponse AssertMatchedResponse(StubMatchResult matched, StubResponse? response)
    {
        Assert.Equal(StubMatchResult.Matched, matched);
        return Assert.IsType<StubResponse>(response);
    }

    private static Task<(StubMatchResult Result, StubResponse? Response)> DispatchAsync(
        StubService service,
        string method,
        string path)
    {
        return DispatchAsync(
            service,
            method,
            path,
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);
    }

    private static Task<(StubMatchResult Result, StubResponse? Response)> DispatchAsync(
        StubService service,
        string method,
        string path,
        IReadOnlyDictionary<string, string> query)
    {
        return DispatchAsync(
            service,
            method,
            path,
            query.ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value), StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);
    }

    private static Task<(StubMatchResult Result, StubResponse? Response)> DispatchAsync(
        StubService service,
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query)
    {
        return DispatchAsync(
            service,
            method,
            path,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);
    }

    private static Task<(StubMatchResult Result, StubResponse? Response)> DispatchAsync(
        StubService service,
        string method,
        string path,
        IReadOnlyDictionary<string, string> query,
        string? body)
    {
        return DispatchAsync(
            service,
            method,
            path,
            query.ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value), StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body);
    }

    private static Task<(StubMatchResult Result, StubResponse? Response)> DispatchAsync(
        StubService service,
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        string? body)
    {
        return DispatchAsync(
            service,
            method,
            path,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body);
    }

    private static async Task<(StubMatchResult Result, StubResponse? Response)> DispatchAsync(
        StubService service,
        string method,
        string path,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        var dispatch = await service.DispatchAsync(method, path, query, headers, body);
        return (dispatch.Result, dispatch.Response);
    }

    private static StubService CreateService(
        StubDocument document,
        ScenarioService? scenarioService = null,
        Func<string, string>? responseFileReader = null,
        MatcherService? matcherService = null,
        ISemanticMatcherService? semanticMatcherService = null)
    {
        var resolvedScenarioService = scenarioService ?? new ScenarioService();
        var resolvedMatcherService = matcherService ?? CreateMatcherService();
        var loader = new TestStubDefinitionLoader(document, responseFileReader ?? ThrowingResponseFileReader);
        var state = new StubDefinitionState(loader, resolvedScenarioService, NullLogger<StubDefinitionState>.Instance);
        var responseBuilder = new StubResponseBuilder(state.LoadResponseFileContent);

        return new StubService(
            state,
            resolvedMatcherService,
            resolvedScenarioService,
            new StubDispatchSelector(
                resolvedMatcherService,
                semanticMatcherService,
                responseBuilder,
                new StubDefaultResponseSelector(responseBuilder, resolvedScenarioService),
                resolvedScenarioService,
                NullLogger<StubDispatchSelector>.Instance),
            new StubInspectionProjectionBuilder(resolvedScenarioService));
    }

    private sealed class TestStubDefinitionLoader(StubDocument document, Func<string, string> responseFileReader) : IStubDefinitionLoader
    {
        public string GetDefinitionsDirectoryPath()
        {
            throw new InvalidOperationException("Definitions directory path is not available in this test loader.");
        }

        public StubDocument LoadDefaultDefinition()
        {
            return document;
        }

        public string LoadResponseFileContent(string fileName)
        {
            return responseFileReader(fileName);
        }
    }

    [Fact]
    public async Task DispatchAsync_AdvancesScenarioStateAcrossRequests()
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
                                },
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new()
                                    {
                                        Example = new Dictionary<object, object>
                                        {
                                            ["result"] = "pending"
                                        }
                                    }
                                }
                            },
                            ["200"] = new()
                            {
                                Scenario = new ScenarioDefinition
                                {
                                    Name = "checkout-flow",
                                    State = "confirmed"
                                },
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new()
                                    {
                                        Example = new Dictionary<object, object>
                                        {
                                            ["result"] = "complete"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService(document);

        var (firstMatch, firstResponse) = await DispatchAsync(service, HttpMethods.Post, "/checkout");
        var firstMatchedResponse = AssertMatchedResponse(firstMatch, firstResponse);
        var (secondMatch, secondResponse) = await DispatchAsync(service, HttpMethods.Post, "/checkout");
        var secondMatchedResponse = AssertMatchedResponse(secondMatch, secondResponse);

        Assert.Equal(409, firstMatchedResponse.StatusCode);
        Assert.Equal("{\"result\":\"pending\"}", firstMatchedResponse.Body);
        Assert.Equal(200, secondMatchedResponse.StatusCode);
        Assert.Equal("{\"result\":\"complete\"}", secondMatchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_FiltersMatchedConditionalResponsesByScenarioState()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/checkout"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Matches =
                        [
                            new QueryMatchDefinition
                                {
                                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                                    {
                                        ["step"] = "1"
                                    },
                                    Response = new QueryMatchResponseDefinition
                                    {
                                        StatusCode = 409,
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
                                                Example = new Dictionary<object, object>
                                                {
                                                    ["result"] = "pending"
                                                }
                                            }
                                        }
                                    }
                                },
                                new QueryMatchDefinition
                                {
                                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                                    {
                                        ["step"] = "1"
                                    },
                                    Response = new QueryMatchResponseDefinition
                                    {
                                        StatusCode = 200,
                                        Scenario = new ScenarioDefinition
                                        {
                                            Name = "checkout-flow",
                                            State = "confirmed"
                                        },
                                        Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                        {
                                            ["application/json"] = new()
                                            {
                                                Example = new Dictionary<object, object>
                                                {
                                                    ["result"] = "complete"
                                                }
                                            }
                                        }
                                    }
                                }
                        ]
                    }
                }
            }
        };

        var service = CreateService(document);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["step"] = "1"
        };

        var (firstMatch, firstResponse) = await DispatchAsync(service, HttpMethods.Post, "/checkout", query);
        var firstMatchedResponse = AssertMatchedResponse(firstMatch, firstResponse);
        var (secondMatch, secondResponse) = await DispatchAsync(service, HttpMethods.Post, "/checkout", query);
        var secondMatchedResponse = AssertMatchedResponse(secondMatch, secondResponse);

        Assert.Equal(409, firstMatchedResponse.StatusCode);
        Assert.Equal(200, secondMatchedResponse.StatusCode);
    }
}
