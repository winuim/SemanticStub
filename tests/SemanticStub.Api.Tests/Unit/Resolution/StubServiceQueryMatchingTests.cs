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

public sealed class StubServiceQueryMatchingTests
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
    public async Task DispatchAsync_MatchesMultiValueQueryParameter()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/search"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Matches =
                        [
                            new QueryMatchDefinition
                                {
                                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                                    {
                                        ["tag"] = new List<object?> { "alpha", "beta" }
                                    },
                                    Response = new QueryMatchResponseDefinition
                                    {
                                        StatusCode = 200,
                                        Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                        {
                                            ["application/json"] = new()
                                            {
                                                Example = new Dictionary<object, object>
                                                {
                                                    ["result"] = "ordered"
                                                }
                                            }
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
                                    ["application/json"] = new()
                                    {
                                        Example = new Dictionary<object, object>
                                        {
                                            ["result"] = "fallback"
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
        var query = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["tag"] = new StringValues(["alpha", "beta"])
        };

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/search", query);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"ordered\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_PrefersMoreSpecificQueryMatch()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/users"] = new()
                {
                    Get = new OperationDefinition
                    {
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
                                            ["application/json"] = new()
                                            {
                                                Example = new Dictionary<object, object>
                                                {
                                                    ["message"] = "admin"
                                                }
                                            }
                                        }
                                    }
                                },
                                new QueryMatchDefinition
                                {
                                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                                    {
                                        ["role"] = "admin",
                                        ["view"] = "summary"
                                    },
                                    Response = new QueryMatchResponseDefinition
                                    {
                                        StatusCode = 200,
                                        Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                        {
                                            ["application/json"] = new()
                                            {
                                                Example = new Dictionary<object, object>
                                                {
                                                    ["message"] = "admin-summary"
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
            ["role"] = "admin",
            ["view"] = "summary"
        };

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/users", query);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"message\":\"admin-summary\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_FallsBackToDefaultResponseWhenNoQueryMatchExists()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/users"] = new()
                {
                    Get = new OperationDefinition
                    {
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
                                            ["application/json"] = new()
                                            {
                                                Example = new Dictionary<object, object>
                                                {
                                                    ["message"] = "admin"
                                                }
                                            }
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
                                    ["application/json"] = new()
                                    {
                                        Example = new Dictionary<object, object>
                                        {
                                            ["message"] = "default"
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
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "guest"
        };

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/users", query);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"message\":\"default\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsResponseNotConfigured_WhenMatchedQueryResponseIsInvalid()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/users"] = new()
                {
                    Get = new OperationDefinition
                    {
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
                                        StatusCode = 0,
                                        Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                        {
                                            ["application/json"] = new()
                                            {
                                                Example = new Dictionary<object, object>
                                                {
                                                    ["message"] = "broken"
                                                }
                                            }
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
                                    ["application/json"] = new()
                                    {
                                        Example = new Dictionary<object, object>
                                        {
                                            ["message"] = "default"
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
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var (matched, _) = await DispatchAsync(service, HttpMethods.Get, "/users", query);

        Assert.Equal(StubMatchResult.ResponseNotConfigured, matched);
    }
}
