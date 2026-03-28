using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class StubServiceTests
{
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

        IStubService service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.NotNull(response);
        Assert.Equal(200, response.StatusCode);
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal("{\"message\":\"Created\"}", response.Body);
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal(250, response.DelayMilliseconds);
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

        var service = new StubService(document);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal(125, response.DelayMilliseconds);
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Null(response.DelayMilliseconds);
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

        var service = new StubService(document);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Null(response.DelayMilliseconds);
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

        var service = new StubService(document);
        var query = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["tag"] = new StringValues(["alpha", "beta"])
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/search", query, out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("{\"result\":\"ordered\"}", response.Body);
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

        var service = new StubService(document, _ => "{\"users\":[{\"id\":1,\"name\":\"Alice\"}]}");

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("{\"users\":[{\"id\":1,\"name\":\"Alice\"}]}", response.Body);
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

            var service = new StubService(document, _ => throw new InvalidOperationException("Response file reader should not be used for absolute paths."));

            var matched = service.TryGetResponse(HttpMethods.Get, "/download", out var response);

            Assert.Equal(StubMatchResult.Matched, matched);
            Assert.Equal(filePath, response.FilePath);
            Assert.Equal(string.Empty, response.Body);
            Assert.Equal("application/octet-stream", response.ContentType);
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("hello", response.Headers["X-Stub-Source"].Single());
        Assert.Equal("123", response.Headers["X-Trace-Id"].Single());
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("2026-03-26T00:00:00.0000000+00:00", response.Headers["Last-Modified"].Single());
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("Accept-Encoding, Origin", response.Headers["Vary"].Single());
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal(new[] { "a=1; Path=/", "b=2; Path=/" }, response.Headers["Set-Cookie"].ToArray());
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

        var service = new StubService(document);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("admin", response.Headers["X-User-Role"].Single());
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Put, "/profile", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("{\"result\":\"replaced\"}", response.Body);
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Delete, "/profile", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("{\"result\":\"deleted\"}", response.Body);
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

        var service = new StubService(document);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin",
            ["view"] = "summary"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("{\"message\":\"admin-summary\"}", response.Body);
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

        var service = new StubService(document);
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "guest"
        };

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("{\"message\":\"default\"}", response.Body);
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

        var service = new StubService(document);
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("text/plain", response.ContentType);
        Assert.Equal("Hello, world!", response.Body);
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

        var service = new StubService(document, _ => "<root><item>1</item></root>");

        var matched = service.TryGetResponse(HttpMethods.Get, "/data", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("application/xml", response.ContentType);
        Assert.Equal("<root><item>1</item></root>", response.Body);
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

        var service = new StubService(document);
        var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["format"] = "csv" };

        var matched = service.TryGetResponse(HttpMethods.Get, "/report", query, out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("text/csv", response.ContentType);
        Assert.Equal("id,name\n1,Alice", response.Body);
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

        var service = new StubService(document);
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(
            HttpMethods.Post,
            "/login",
            new Dictionary<string, string>(StringComparer.Ordinal),
            "{\"username\":\"demo\",\"password\":\"secret\",\"rememberMe\":true}",
            out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("{\"result\":\"ok\"}", response.Body);
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(
            HttpMethods.Post,
            "/login",
            new Dictionary<string, string>(StringComparer.Ordinal),
            "{\"username\":\"demo\",\"password\":\"secret\"}",
            out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("{\"result\":\"specific\"}", response.Body);
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(
            HttpMethods.Post,
            "/login",
            new Dictionary<string, string>(StringComparer.Ordinal),
            "{not-json",
            out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("{\"result\":\"fallback\"}", response.Body);
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

        var service = new StubService(document);

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

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("{\"message\":\"staging\"}", response.Body);
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Get, "/orders/123", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("{\"result\":\"pattern\"}", response.Body);
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Get, "/orders/special", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("{\"result\":\"exact\"}", response.Body);
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

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Post, "/orders/123", out _);

        Assert.Equal(StubMatchResult.MethodNotAllowed, matched);
    }
}
