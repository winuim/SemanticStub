using Microsoft.AspNetCore.Http;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class StubServiceTests
{
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
                                Query = new Dictionary<string, string>(StringComparer.Ordinal)
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
                                Query = new Dictionary<string, string>(StringComparer.Ordinal)
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
                                Query = new Dictionary<string, string>(StringComparer.Ordinal)
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
                                Query = new Dictionary<string, string>(StringComparer.Ordinal)
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
    public void TryGetResponse_ReturnsResponseNotConfigured_WhenFallbackResponseHasNoJsonContent()
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
                                Query = new Dictionary<string, string>(StringComparer.Ordinal)
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
                                    ["text/plain"] = new()
                                    {
                                        Example = "default"
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

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", query, out _);

        Assert.Equal(StubMatchResult.ResponseNotConfigured, matched);
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
                                Query = new Dictionary<string, string>(StringComparer.Ordinal)
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
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-env"] = "staging"
            },
            body: null,
            out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal("{\"message\":\"staging\"}", response.Body);
    }
}
