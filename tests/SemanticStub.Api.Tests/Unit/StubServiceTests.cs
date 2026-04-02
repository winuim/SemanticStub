using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class StubServiceTests
{
    private static StubResponse AssertMatchedResponse(StubMatchResult matched, StubResponse? response)
    {
        Assert.Equal(StubMatchResult.Matched, matched);
        return Assert.IsType<StubResponse>(response);
    }

    [Fact]
    public void InterfaceContract_ExposesConvenienceOverloadForMethodAndPathOnly()
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

        IStubService service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(200, matchedResponse.StatusCode);
    }

    [Fact]
    public void InterfaceContract_PreservesLoaderAndMatcherConstructorCompatibility()
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

        var loader = new TestStubDefinitionLoader(document);
        var service = new StubService(loader, new MatcherService(), new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(200, matchedResponse.StatusCode);
    }

    [Fact]
    public void TryGetResponse_UsesStatusCodeDefinedInYamlResponses()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(201, matchedResponse.StatusCode);
        Assert.Equal("{\"message\":\"Created\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_ReturnsConfiguredDelayFromYamlResponses()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(250, matchedResponse.DelayMilliseconds);
    }

    [Fact]
    public void TryGetResponse_ReturnsConfiguredDelayFromMatchedResponse()
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

        var service = new StubService(document, new ScenarioService());
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(125, matchedResponse.DelayMilliseconds);
    }

    [Fact]
    public void TryGetResponse_LeavesDelayNullWhenYamlResponseDelayIsNotConfigured()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Null(matchedResponse.DelayMilliseconds);
    }

    [Fact]
    public void TryGetResponse_LeavesDelayNullWhenMatchedResponseDelayIsNotConfigured()
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

        var service = new StubService(document, new ScenarioService());
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Null(matchedResponse.DelayMilliseconds);
    }

    [Fact]
    public void TryGetResponse_MatchesMultiValueQueryParameter()
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

        var service = new StubService(document, new ScenarioService());
        var query = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["tag"] = new StringValues(["alpha", "beta"])
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/search", query, out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"ordered\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_UsesResponseFileContentWhenConfigured()
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

        var service = new StubService(document, _ => "{\"users\":[{\"id\":1,\"name\":\"Alice\"}]}", new MatcherService(), new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(200, matchedResponse.StatusCode);
        Assert.Equal("{\"users\":[{\"id\":1,\"name\":\"Alice\"}]}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_UsesFilePathForAbsoluteResponseFile()
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

            var service = new StubService(document, _ => throw new InvalidOperationException("Response file reader should not be used for absolute paths."), new MatcherService(), new ScenarioService());

            var matched = service.TryGetResponse(HttpMethods.Get, "/download", out var response);
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
    public void TryGetResponse_ReturnsConfiguredResponseHeaders()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("hello", matchedResponse.Headers["X-Stub-Source"].Single());
        Assert.Equal("123", matchedResponse.Headers["X-Trace-Id"].Single());
    }

    [Fact]
    public void TryGetResponse_ReturnsDateTimeHeaderExamplesWithoutJsonQuoting()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("2026-03-26T00:00:00.0000000+00:00", matchedResponse.Headers["Last-Modified"].Single());
    }

    [Fact]
    public void TryGetResponse_JoinsArrayHeaderExamplesUsingHttpHeaderFormatting()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("Accept-Encoding, Origin", matchedResponse.Headers["Vary"].Single());
    }

    [Fact]
    public void TryGetResponse_PreservesSeparateSetCookieHeaderValues()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(new[] { "a=1; Path=/", "b=2; Path=/" }, matchedResponse.Headers["Set-Cookie"].ToArray());
    }

    [Fact]
    public void TryGetResponse_ReturnsConfiguredHeadersFromMatchedResponse()
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

        var service = new StubService(document, new ScenarioService());
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("admin", matchedResponse.Headers["X-User-Role"].Single());
    }

    [Fact]
    public void TryGetResponse_UsesPutOperationWhenConfigured()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Put, "/profile", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"replaced\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_UsesDeleteOperationWhenConfigured()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Delete, "/profile", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"deleted\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_PrefersMoreSpecificQueryMatch()
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

        var service = new StubService(document, new ScenarioService());
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin",
            ["view"] = "summary"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"message\":\"admin-summary\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_FallsBackToDefaultResponseWhenNoQueryMatchExists()
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

        var service = new StubService(document, new ScenarioService());
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "guest"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"message\":\"default\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_ReturnsResponseNotConfigured_WhenMatchedQueryResponseIsInvalid()
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

        var service = new StubService(document, new ScenarioService());
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out _);

        Assert.Equal(StubMatchResult.ResponseNotConfigured, matched);
    }

    [Fact]
    public void TryGetResponse_ReturnsTextPlainBodyWithCorrectContentType()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("text/plain", matchedResponse.ContentType);
        Assert.Equal("Hello, world!", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_ReturnsXmlContentTypeForResponseFile()
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

        var service = new StubService(document, _ => "<root><item>1</item></root>", new MatcherService(), new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/data", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("application/xml", matchedResponse.ContentType);
        Assert.Equal("<root><item>1</item></root>", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_ReturnsNonJsonContentTypeFromXMatch()
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

        var service = new StubService(document, new ScenarioService());
        var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["format"] = "csv" };

        var matched = service.TryGetResponse(HttpMethods.Get, "/report", query, out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("text/csv", matchedResponse.ContentType);
        Assert.Equal("id,name\n1,Alice", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_ReturnsResponseNotConfigured_WhenFallbackJsonExampleIsMissing()
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

        var service = new StubService(document, new ScenarioService());
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "guest"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out _);

        Assert.Equal(StubMatchResult.ResponseNotConfigured, matched);
    }

    [Fact]
    public void TryGetResponse_MatchesResponseUsingRequestBody()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(
            HttpMethods.Post,
            "/login",
            new Dictionary<string, string>(StringComparer.Ordinal),
            "{\"username\":\"demo\",\"password\":\"secret\",\"rememberMe\":true}",
            out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"ok\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_PrefersMoreSpecificBodyMatch()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(
            HttpMethods.Post,
            "/login",
            new Dictionary<string, string>(StringComparer.Ordinal),
            "{\"username\":\"demo\",\"password\":\"secret\"}",
            out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"specific\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_FallsBackToDefaultResponseWhenBodyIsInvalidJson()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(
            HttpMethods.Post,
            "/login",
            new Dictionary<string, string>(StringComparer.Ordinal),
            "{not-json",
            out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"fallback\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_MatchesResponseUsingHeaders()
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
                                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(
            HttpMethods.Get,
            "/users",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-env"] = "staging"
            },
            body: null,
            out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"message\":\"staging\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_MatchesOpenApiPathTemplateWhenExactPathIsMissing()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/orders/123", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"pattern\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_PrefersExactPathOverMatchingTemplatePath()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/orders/special", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal("{\"result\":\"exact\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_ReturnsMethodNotAllowedForMatchingTemplatePathWithUnsupportedMethod()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Post, "/orders/123", out _);

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

        var service = new StubService(document, new ScenarioService());

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

        var service = new StubService(document, new ScenarioService());

        var allowedMethods = service.GetAllowedMethods("/orders/123");

        Assert.Equal([HttpMethods.Get, HttpMethods.Patch], allowedMethods);
    }

    private sealed class TestStubDefinitionLoader(StubDocument document) : IStubDefinitionLoader
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
            throw new InvalidOperationException("Response-file loading is not used in this test.");
        }
    }

    private sealed class SpySemanticMatcherService(QueryMatchDefinition? nextMatch = null) : ISemanticMatcherService
    {
        public int CallCount { get; private set; }

        public QueryMatchDefinition? FindBestMatch(
            string method,
            string path,
            IReadOnlyDictionary<string, StringValues> query,
            IReadOnlyDictionary<string, string> headers,
            string? body,
            IReadOnlyCollection<QueryMatchDefinition> candidates,
            Func<QueryMatchDefinition, bool>? candidateFilter = null)
        {
            CallCount++;

            if (nextMatch is null)
            {
                return null;
            }

            return candidateFilter is null || candidateFilter(nextMatch)
                ? nextMatch
                : null;
        }
    }

    [Fact]
    public void TryGetResponse_UsesSemanticFallbackWhenDeterministicMatchFails()
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
        var service = new StubService(
            document,
            _ => throw new InvalidOperationException("No response file loading expected."),
            new MatcherService(),
            new ScenarioService(),
            semanticMatcher);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "guest"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(1, semanticMatcher.CallCount);
        Assert.Equal(202, matchedResponse.StatusCode);
        Assert.Equal("{\"message\":\"semantic-admin\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_DoesNotUseSemanticFallbackWhenDeterministicMatchSucceeds()
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
        var service = new StubService(
            document,
            _ => throw new InvalidOperationException("No response file loading expected."),
            new MatcherService(),
            new ScenarioService(),
            semanticMatcher);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(0, semanticMatcher.CallCount);
        Assert.Equal(200, matchedResponse.StatusCode);
        Assert.Equal("{\"message\":\"admin\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_DoesNotTreatSemanticOnlyCandidateAsDeterministicMatch()
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

        var service = new StubService(document, new ScenarioService());

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", out var response);
        var matchedResponse = AssertMatchedResponse(matched, response);

        Assert.Equal(404, matchedResponse.StatusCode);
        Assert.Equal("{\"message\":\"default\"}", matchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_AdvancesScenarioStateAcrossRequests()
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

        var service = new StubService(document, new ScenarioService());

        var firstMatch = service.TryGetResponse(HttpMethods.Post, "/checkout", out var firstResponse);
        var firstMatchedResponse = AssertMatchedResponse(firstMatch, firstResponse);
        var secondMatch = service.TryGetResponse(HttpMethods.Post, "/checkout", out var secondResponse);
        var secondMatchedResponse = AssertMatchedResponse(secondMatch, secondResponse);

        Assert.Equal(409, firstMatchedResponse.StatusCode);
        Assert.Equal("{\"result\":\"pending\"}", firstMatchedResponse.Body);
        Assert.Equal(200, secondMatchedResponse.StatusCode);
        Assert.Equal("{\"result\":\"complete\"}", secondMatchedResponse.Body);
    }

    [Fact]
    public void TryGetResponse_FiltersMatchedConditionalResponsesByScenarioState()
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

        var service = new StubService(document, new ScenarioService());
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["step"] = "1"
        };

        var firstMatch = service.TryGetResponse(HttpMethods.Post, "/checkout", query, out var firstResponse);
        var firstMatchedResponse = AssertMatchedResponse(firstMatch, firstResponse);
        var secondMatch = service.TryGetResponse(HttpMethods.Post, "/checkout", query, out var secondResponse);
        var secondMatchedResponse = AssertMatchedResponse(secondMatch, secondResponse);

        Assert.Equal(409, firstMatchedResponse.StatusCode);
        Assert.Equal(200, secondMatchedResponse.StatusCode);
    }
}
