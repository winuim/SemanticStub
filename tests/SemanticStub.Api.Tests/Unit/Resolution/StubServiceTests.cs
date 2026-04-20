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

public sealed class StubServiceTests
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

    [Fact]
    public async Task DispatchAsync_ReturnsMatchedResponseForMethodAndPathOnly()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = new OperationDefinition
                    {
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
                                            ["message"] = "Hello"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        StubService service = CreateService(document);

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/hello");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(200, matchedResponse.StatusCode);
    }

    [Fact]
    public void InterfaceContract_OnlyExposesProductionUsedMembers()
    {
        var publicMethods = typeof(IStubService)
            .GetMethods()
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                nameof(IStubService.DispatchAsync),
                nameof(IStubService.ExplainMatchAsync),
                nameof(IStubService.GetAllowedMethods)
            ],
            publicMethods);
    }

    [Fact]
    public void ConstructorContract_DoesNotExposePublicConstructors()
    {
        Assert.Empty(typeof(StubService).GetConstructors());
    }

    [Fact]
    public async Task ConstructorContract_UsesLoaderBackedDocumentState()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = new OperationDefinition
                    {
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
                                            ["message"] = "Hello"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService(document, matcherService: CreateMatcherService());

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/hello");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(200, matchedResponse.StatusCode);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsMatchedResponseContract()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = new OperationDefinition
                    {
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
                                            ["message"] = "Hello"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        StubService service = CreateService(document);

        var dispatch = await service.DispatchAsync(
            HttpMethods.Get,
            "/hello",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        var matchedResponse = AssertMatchedResponse(dispatch.Result, dispatch.Response);

        Assert.Equal(200, matchedResponse.StatusCode);
    }

    [Fact]
    public async Task DispatchAsync_QueryOnlyOverload_MatchesFullOverloadBehavior()
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
                        ]
                    }
                }
            }
        };

        var service = CreateService(document);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var (queryOnlyResult, queryOnlyResponse) = await DispatchAsync(service, HttpMethods.Get, "/users", query);
        var (fullResult, fullResponse) = await DispatchAsync(
            service,
            HttpMethods.Get,
            "/users",
            query.ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value), StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        var queryOnlyMatchedResponse = AssertMatchedResponse(queryOnlyResult, queryOnlyResponse);
        var fullMatchedResponse = AssertMatchedResponse(fullResult, fullResponse);

        Assert.Equal(fullMatchedResponse.StatusCode, queryOnlyMatchedResponse.StatusCode);
        Assert.Equal(fullMatchedResponse.Body, queryOnlyMatchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_UsesStatusCodeDefinedInYamlResponses()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["201"] = new()
                            {
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new()
                                    {
                                        Example = new Dictionary<object, object>
                                        {
                                            ["message"] = "Created"
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

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/hello");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(201, matchedResponse.StatusCode);
        Assert.Equal("{\"message\":\"Created\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsConfiguredDelayFromYamlResponses()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new()
                            {
                                DelayMilliseconds = 250,
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new()
                                    {
                                        Example = new Dictionary<object, object>
                                        {
                                            ["message"] = "Hello"
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

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/hello");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(250, matchedResponse.DelayMilliseconds);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsConfiguredDelayFromMatchedResponse()
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
                                    DelayMilliseconds = 125,
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
                        ]
                    }
                }
            }
        };

        var service = CreateService(document);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/users", query);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(125, matchedResponse.DelayMilliseconds);
    }

    [Fact]
    public async Task DispatchAsync_LeavesDelayNullWhenYamlResponseDelayIsNotConfigured()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = new OperationDefinition
                    {
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
                                            ["message"] = "Hello"
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

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/hello");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Null(matchedResponse.DelayMilliseconds);
    }

    [Fact]
    public async Task DispatchAsync_LeavesDelayNullWhenMatchedResponseDelayIsNotConfigured()
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
                        ]
                    }
                }
            }
        };

        var service = CreateService(document);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/users", query);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Null(matchedResponse.DelayMilliseconds);
    }

    [Fact]
    public async Task DispatchAsync_UsesResponseFileContentWhenConfigured()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/users"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new()
                            {
                                ResponseFile = "users.json",
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new()
                                }
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService(
            document,
            responseFileReader: _ => "{\"users\":[{\"id\":1,\"name\":\"Alice\"}]}",
            matcherService: CreateMatcherService());

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/users");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(200, matchedResponse.StatusCode);
        Assert.Equal("{\"users\":[{\"id\":1,\"name\":\"Alice\"}]}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_UsesFilePathForAbsoluteResponseFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"semanticstub-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(filePath, [0x00, 0x01, 0x7F, 0xFF]);

        try
        {
            var document = new StubDocument
            {
                Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
                {
                    ["/download"] = new()
                    {
                        Get = new OperationDefinition
                        {
                            Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                            {
                                ["200"] = new()
                                {
                                    ResponseFile = filePath,
                                    Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                    {
                                        ["application/octet-stream"] = new()
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var service = CreateService(
                document,
                responseFileReader: _ => throw new InvalidOperationException("Response file reader should not be used for absolute paths."),
                matcherService: CreateMatcherService());

            var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/download");
            var matchedResponse = AssertMatchedResponse(matched, response);

            Assert.Equal(filePath, matchedResponse.FilePath);
            Assert.Equal(string.Empty, matchedResponse.Body);
            Assert.Equal("application/octet-stream", matchedResponse.ContentType);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task DispatchAsync_ReturnsConfiguredResponseHeaders()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new()
                            {
                                Headers = new Dictionary<string, HeaderDefinition>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["X-Stub-Source"] = new()
                                    {
                                        Example = "hello"
                                    },
                                    ["X-Trace-Id"] = new()
                                    {
                                        Schema = new HeaderSchemaDefinition
                                        {
                                            Example = 123
                                        }
                                    }
                                },
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new()
                                    {
                                        Example = new Dictionary<object, object>
                                        {
                                            ["message"] = "Hello"
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

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/hello");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("hello", matchedResponse.Headers["X-Stub-Source"].Single());
        Assert.Equal("123", matchedResponse.Headers["X-Trace-Id"].Single());
    }

    [Fact]
    public async Task DispatchAsync_ReturnsDateTimeHeaderExamplesWithoutJsonQuoting()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new()
                            {
                                Headers = new Dictionary<string, HeaderDefinition>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["Last-Modified"] = new()
                                    {
                                        Example = new DateTimeOffset(2026, 3, 26, 0, 0, 0, TimeSpan.Zero)
                                    }
                                },
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new()
                                    {
                                        Example = new Dictionary<object, object>
                                        {
                                            ["message"] = "Hello"
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

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/hello");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("2026-03-26T00:00:00.0000000+00:00", matchedResponse.Headers["Last-Modified"].Single());
    }

    [Fact]
    public async Task DispatchAsync_JoinsArrayHeaderExamplesUsingHttpHeaderFormatting()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new()
                            {
                                Headers = new Dictionary<string, HeaderDefinition>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["Vary"] = new()
                                    {
                                        Schema = new HeaderSchemaDefinition
                                        {
                                            Example = new List<object> { "Accept-Encoding", "Origin" }
                                        }
                                    }
                                },
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new()
                                    {
                                        Example = new Dictionary<object, object>
                                        {
                                            ["message"] = "Hello"
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

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/hello");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("Accept-Encoding, Origin", matchedResponse.Headers["Vary"].Single());
    }

    [Fact]
    public async Task DispatchAsync_PreservesSeparateSetCookieHeaderValues()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new()
                            {
                                Headers = new Dictionary<string, HeaderDefinition>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["Set-Cookie"] = new()
                                    {
                                        Schema = new HeaderSchemaDefinition
                                        {
                                            Example = new List<object> { "a=1; Path=/", "b=2; Path=/" }
                                        }
                                    }
                                },
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new()
                                    {
                                        Example = new Dictionary<object, object>
                                        {
                                            ["message"] = "Hello"
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

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/hello");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(new[] { "a=1; Path=/", "b=2; Path=/" }, matchedResponse.Headers["Set-Cookie"].ToArray());
    }

    [Fact]
    public async Task DispatchAsync_ReturnsConfiguredHeadersFromMatchedResponse()
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
                                    Headers = new Dictionary<string, HeaderDefinition>(StringComparer.OrdinalIgnoreCase)
                                    {
                                        ["X-User-Role"] = new()
                                        {
                                            Example = "admin"
                                        }
                                    },
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
                        ]
                    }
                }
            }
        };

        var service = CreateService(document);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/users", query);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("admin", matchedResponse.Headers["X-User-Role"].Single());
    }

    [Fact]
    public async Task DispatchAsync_UsesPutOperationWhenConfigured()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/profile"] = new()
                {
                    Put = new OperationDefinition
                    {
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
                                            ["result"] = "replaced"
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

        var (matched, response) = await DispatchAsync(service, HttpMethods.Put, "/profile");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"replaced\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_UsesDeleteOperationWhenConfigured()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/profile"] = new()
                {
                    Delete = new OperationDefinition
                    {
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
                                            ["result"] = "deleted"
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

        var (matched, response) = await DispatchAsync(service, HttpMethods.Delete, "/profile");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"deleted\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsTextPlainBodyWithCorrectContentType()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new()
                            {
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["text/plain"] = new()
                                    {
                                        Example = "Hello, world!"
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService(document);

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/hello");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("text/plain", matchedResponse.ContentType);
        Assert.Equal("Hello, world!", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsXmlContentTypeForResponseFile()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/data"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new()
                            {
                                ResponseFile = "data.xml",
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/xml"] = new()
                                }
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService(
            document,
            responseFileReader: _ => "<root><item>1</item></root>",
            matcherService: CreateMatcherService());

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/data");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("application/xml", matchedResponse.ContentType);
        Assert.Equal("<root><item>1</item></root>", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsNonJsonContentTypeFromXMatch()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/report"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Matches =
                        [
                            new QueryMatchDefinition
                            {
                                Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                                {
                                    ["format"] = "csv"
                                },
                                Response = new QueryMatchResponseDefinition
                                {
                                    StatusCode = 200,
                                    Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                    {
                                        ["text/csv"] = new()
                                        {
                                            Example = "id,name\n1,Alice"
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
        var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["format"] = "csv" };

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/report", query);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("text/csv", matchedResponse.ContentType);
        Assert.Equal("id,name\n1,Alice", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsResponseNotConfigured_WhenFallbackJsonExampleIsMissing()
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

        var (matched, _) = await DispatchAsync(service, HttpMethods.Get, "/users", query);

        Assert.Equal(StubMatchResult.ResponseNotConfigured, matched);
    }

    [Fact]
    public async Task DispatchAsync_MatchesResponseUsingRequestBody()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/login"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Matches =
                        [
                            new QueryMatchDefinition
                            {
                                Body = new Dictionary<object, object>
                                {
                                    ["username"] = "demo",
                                    ["password"] = "secret"
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
                                                ["result"] = "ok"
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

        var (matched, response) = await DispatchAsync(
            service,
            HttpMethods.Post,
            "/login",
            new Dictionary<string, string>(StringComparer.Ordinal),
            "{\"username\":\"demo\",\"password\":\"secret\",\"rememberMe\":true}");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"ok\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_PrefersMoreSpecificBodyMatch()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/login"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Matches =
                        [
                            new QueryMatchDefinition
                            {
                                Body = new Dictionary<object, object>
                                {
                                    ["username"] = "demo"
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
                                                ["result"] = "basic"
                                            }
                                        }
                                    }
                                }
                            },
                            new QueryMatchDefinition
                            {
                                Body = new Dictionary<object, object>
                                {
                                    ["username"] = "demo",
                                    ["password"] = "secret"
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
                                                ["result"] = "specific"
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

        var (matched, response) = await DispatchAsync(
            service,
            HttpMethods.Post,
            "/login",
            new Dictionary<string, string>(StringComparer.Ordinal),
            "{\"username\":\"demo\",\"password\":\"secret\"}");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"specific\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_FallsBackToDefaultResponseWhenBodyIsInvalidJson()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/login"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Matches =
                        [
                            new QueryMatchDefinition
                            {
                                Body = new Dictionary<object, object>
                                {
                                    ["username"] = "demo"
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
                                                ["result"] = "matched"
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

        var (matched, response) = await DispatchAsync(
            service,
            HttpMethods.Post,
            "/login",
            new Dictionary<string, string>(StringComparer.Ordinal),
            "{not-json");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"fallback\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_MatchesResponseUsingHeaders()
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
                                Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["X-Env"] = "staging"
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
                                                ["message"] = "staging"
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

        var (matched, response) = await DispatchAsync(
            service,
            HttpMethods.Get,
            "/users",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-env"] = "staging"
            },
            body: null);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"message\":\"staging\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_MatchesOpenApiPathTemplateWhenExactPathIsMissing()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/orders/{orderId}"] = new()
                {
                    Get = new OperationDefinition
                    {
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
                                            ["result"] = "pattern"
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

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/orders/123");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"pattern\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_PrefersExactPathOverMatchingTemplatePath()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/orders/{orderId}"] = new()
                {
                    Get = new OperationDefinition
                    {
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
                                            ["result"] = "pattern"
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                ["/orders/special"] = new()
                {
                    Get = new OperationDefinition
                    {
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
                                            ["result"] = "exact"
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

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/orders/special");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"exact\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsMethodNotAllowedForMatchingTemplatePathWithUnsupportedMethod()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/orders/{orderId}"] = new()
                {
                    Get = new OperationDefinition
                    {
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
                                            ["result"] = "pattern"
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

        var (matched, _) = await DispatchAsync(service, HttpMethods.Post, "/orders/123");

        Assert.Equal(StubMatchResult.MethodNotAllowed, matched);
    }

    [Fact]
    public void GetAllowedMethods_ReturnsConfiguredMethodsForExactPathInStableOrder()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/users"] = new()
                {
                    Post = new OperationDefinition(),
                    Get = new OperationDefinition(),
                    Delete = new OperationDefinition()
                }
            }
        };

        var service = CreateService(document);

        var allowedMethods = service.GetAllowedMethods("/users");

        Assert.Equal([HttpMethods.Get, HttpMethods.Post, HttpMethods.Delete], allowedMethods);
    }

    [Fact]
    public void GetAllowedMethods_ReturnsConfiguredMethodsForTemplatePath()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/orders/{orderId}"] = new()
                {
                    Get = new OperationDefinition(),
                    Patch = new OperationDefinition()
                }
            }
        };

        var service = CreateService(document);

        var allowedMethods = service.GetAllowedMethods("/orders/123");

        Assert.Equal([HttpMethods.Get, HttpMethods.Patch], allowedMethods);
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

    private sealed class SpySemanticMatcherService(QueryMatchDefinition? nextMatch = null) : ISemanticMatcherService
    {
        public int CallCount { get; private set; }

        public Task<SemanticMatchExplanation> ExplainMatchAsync(
            string method,
            string path,
            IReadOnlyDictionary<string, StringValues> query,
            IReadOnlyDictionary<string, string> headers,
            string? body,
            IReadOnlyCollection<QueryMatchDefinition> candidates,
            Func<QueryMatchDefinition, bool>? candidateFilter = null,
            bool includeCandidateScores = false,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (nextMatch is null)
            {
                return Task.FromResult(new SemanticMatchExplanation { Attempted = true });
            }

            var result = candidateFilter is null || candidateFilter(nextMatch)
                ? nextMatch
                : null;

            return Task.FromResult(new SemanticMatchExplanation
            {
                Attempted = true,
                SelectedCandidate = result,
            });
        }
    }

    [Fact]
    public async Task DispatchAsync_UsesSemanticFallbackWhenDeterministicMatchFails()
    {
        var semanticCandidate = new QueryMatchDefinition
        {
            Query = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = "admin"
            },
            SemanticMatch = "find admin users",
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 202,
                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                {
                    ["application/json"] = new()
                    {
                        Example = new Dictionary<object, object>
                        {
                            ["message"] = "semantic-admin"
                        }
                    }
                }
            }
        };

        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/users"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Matches = [semanticCandidate]
                    }
                }
            }
        };

        var semanticMatcher = new SpySemanticMatcherService(semanticCandidate);
        var service = CreateService(document, semanticMatcherService: semanticMatcher);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "guest"
        };

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/users", query);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(1, semanticMatcher.CallCount);
        Assert.Equal(202, matchedResponse.StatusCode);
        Assert.Equal("{\"message\":\"semantic-admin\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_DoesNotUseSemanticFallbackWhenDeterministicMatchSucceeds()
    {
        var semanticCandidate = new QueryMatchDefinition
        {
            Query = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = "admin"
            },
            SemanticMatch = "find admin users",
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
        };

        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/users"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Matches = [semanticCandidate]
                    }
                }
            }
        };

        var semanticMatcher = new SpySemanticMatcherService(semanticCandidate);
        var service = CreateService(document, semanticMatcherService: semanticMatcher);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/users", query);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(0, semanticMatcher.CallCount);
        Assert.Equal(200, matchedResponse.StatusCode);
        Assert.Equal("{\"message\":\"admin\"}", matchedResponse.Body);
    }

    [Fact]
    public async Task DispatchAsync_DoesNotTreatSemanticOnlyCandidateAsDeterministicMatch()
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
                                SemanticMatch = "find admin users",
                                Response = new QueryMatchResponseDefinition
                                {
                                    StatusCode = 202,
                                    Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                    {
                                        ["application/json"] = new()
                                        {
                                            Example = new Dictionary<object, object>
                                            {
                                                ["message"] = "semantic-admin"
                                            }
                                        }
                                    }
                                }
                            }
                        ],
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["404"] = new()
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

        var (matched, response) = await DispatchAsync(service, HttpMethods.Get, "/users");
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(404, matchedResponse.StatusCode);
        Assert.Equal("{\"message\":\"default\"}", matchedResponse.Body);
    }

}
