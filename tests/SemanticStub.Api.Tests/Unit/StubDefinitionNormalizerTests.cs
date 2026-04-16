using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class StubDefinitionNormalizerTests
{
    [Fact]
    public void NormalizeDocument_PreservesDocumentShapeAndParameterSchemas()
    {
        var normalizer = new StubDefinitionNormalizer();
        var document = new StubDocument
        {
            OpenApi = "3.1.0",
            Paths = new(StringComparer.Ordinal)
            {
                ["/users"] = new()
                {
                    Parameters =
                    [
                        new()
                        {
                            Name = "trace",
                            In = "query",
                            Schema = new()
                            {
                                Type = "string"
                            }
                        }
                    ],
                    Get = new()
                    {
                        OperationId = "listUsers",
                        Parameters =
                        [
                            new()
                            {
                                Name = "page",
                                In = "query",
                                Schema = new()
                                {
                                    Type = "integer"
                                }
                            }
                        ],
                        Responses = new(StringComparer.Ordinal)
                        {
                            ["200"] = CreateResponse()
                        }
                    }
                }
            }
        };

        var normalized = normalizer.NormalizeDocument(document, Directory.GetCurrentDirectory());

        Assert.Equal("3.1.0", normalized.OpenApi);
        var path = normalized.Paths["/users"];
        var pathParameter = Assert.Single(path.Parameters);
        Assert.Equal("trace", pathParameter.Name);
        Assert.Equal("query", pathParameter.In);
        Assert.Equal("string", pathParameter.Schema?.Type);

        var operation = Assert.IsType<OperationDefinition>(path.Get);
        Assert.Equal("listUsers", operation.OperationId);
        var operationParameter = Assert.Single(operation.Parameters);
        Assert.Equal("page", operationParameter.Name);
        Assert.Equal("integer", operationParameter.Schema?.Type);
    }

    [Fact]
    public void NormalizeDocument_NormalizesMatchQueryHeadersAndBodyValues()
    {
        var normalizer = new StubDefinitionNormalizer();
        var document = CreateDocument(new()
        {
            Matches =
            [
                new()
                {
                    Query = new(StringComparer.Ordinal)
                    {
                        ["filter"] = new Dictionary<object, object>
                        {
                            ["equals"] = "active"
                        },
                        ["tag"] = new List<object>
                        {
                            "alpha",
                            1
                        }
                    },
                    Headers = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["X-Env"] = new Dictionary<object, object>
                        {
                            ["regex"] = "^stage"
                        }
                    },
                    Body = new Dictionary<object, object>
                    {
                        ["form"] = new Dictionary<object, object>
                        {
                            ["username"] = "demo"
                        }
                    },
                    Response = CreateMatchResponse()
                }
            ]
        });

        var normalized = normalizer.NormalizeDocument(document, Directory.GetCurrentDirectory());
        var match = Assert.Single(normalized.Paths["/hello"].Get!.Matches);

        var filter = Assert.IsType<Dictionary<string, object?>>(match.Query["filter"]);
        Assert.Equal("active", filter["equals"]);

        var tag = Assert.IsType<List<object?>>(match.Query["tag"]);
        Assert.Equal(["alpha", 1], tag);

        var header = Assert.IsType<Dictionary<string, object?>>(match.Headers["x-env"]);
        Assert.Equal("^stage", header["regex"]);

        var body = Assert.IsType<Dictionary<string, object?>>(match.Body);
        var form = Assert.IsType<Dictionary<string, object?>>(body["form"]);
        Assert.Equal("demo", form["username"]);
    }

    [Fact]
    public void NormalizeDocument_PreservesSemanticMatchAndMatchResponseDetails()
    {
        var normalizer = new StubDefinitionNormalizer();
        var definitionDirectory = Directory.GetCurrentDirectory();
        var document = CreateDocument(new()
        {
            Matches =
            [
                new()
                {
                    SemanticMatch = "find active users",
                    Response = new()
                    {
                        StatusCode = 202,
                        DelayMilliseconds = 50,
                        ResponseFile = "responses/active-users.json",
                        Headers = new(StringComparer.OrdinalIgnoreCase)
                        {
                            ["X-Stub-Source"] = new()
                            {
                                Example = "matched"
                            }
                        },
                        Scenario = new()
                        {
                            Name = "user-search",
                            State = "initial",
                            Next = "done"
                        },
                        Content = new(StringComparer.Ordinal)
                        {
                            ["application/json"] = new()
                        }
                    }
                }
            ]
        });

        var normalized = normalizer.NormalizeDocument(document, definitionDirectory);
        var match = Assert.Single(normalized.Paths["/hello"].Get!.Matches);

        Assert.Equal("find active users", match.SemanticMatch);
        Assert.Equal(202, match.Response.StatusCode);
        Assert.Equal(50, match.Response.DelayMilliseconds);
        Assert.Equal(Path.Combine(definitionDirectory, "responses", "active-users.json"), match.Response.ResponseFile);
        Assert.Equal("matched", match.Response.Headers["x-stub-source"].Example);
        Assert.Equal("user-search", match.Response.Scenario?.Name);
        Assert.Equal("initial", match.Response.Scenario?.State);
        Assert.Equal("done", match.Response.Scenario?.Next);
        Assert.True(match.Response.Content.ContainsKey("application/json"));
    }

    [Fact]
    public void NormalizeDocument_NormalizesResponses()
    {
        var normalizer = new StubDefinitionNormalizer();
        var definitionDirectory = Directory.GetCurrentDirectory();
        var document = CreateDocument(new()
        {
            Responses = new(StringComparer.Ordinal)
            {
                ["200"] = new()
                {
                    Description = "ok",
                    DelayMilliseconds = 25,
                    ResponseFile = "payloads/users.json",
                    Headers = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["X-Stub-Source"] = new()
                        {
                            Example = "default"
                        }
                    },
                    Scenario = new()
                    {
                        Name = "users",
                        State = "ready",
                        Next = "served"
                    },
                    Content = new(StringComparer.Ordinal)
                    {
                        ["application/json"] = new()
                        {
                            Example = new Dictionary<object, object>
                            {
                                ["users"] = new List<object>()
                            }
                        }
                    }
                }
            }
        });

        var normalized = normalizer.NormalizeDocument(document, definitionDirectory);
        var response = normalized.Paths["/hello"].Get!.Responses["200"];

        Assert.Equal("ok", response.Description);
        Assert.Equal(25, response.DelayMilliseconds);
        Assert.Equal(Path.Combine(definitionDirectory, "payloads", "users.json"), response.ResponseFile);
        Assert.Equal("default", response.Headers["x-stub-source"].Example);
        Assert.Equal("users", response.Scenario?.Name);
        Assert.Equal("ready", response.Scenario?.State);
        Assert.Equal("served", response.Scenario?.Next);
        Assert.True(response.Content.ContainsKey("application/json"));
    }

    [Fact]
    public void NormalizeDocument_PreservesNullOperations()
    {
        var normalizer = new StubDefinitionNormalizer();
        var document = new StubDocument
        {
            OpenApi = "3.1.0",
            Paths = new(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Post = new()
                    {
                        Responses = new(StringComparer.Ordinal)
                        {
                            ["201"] = CreateResponse()
                        }
                    }
                }
            }
        };

        var normalized = normalizer.NormalizeDocument(document, Directory.GetCurrentDirectory());
        var path = normalized.Paths["/hello"];

        Assert.Null(path.Get);
        Assert.NotNull(path.Post);
        Assert.Null(path.Put);
        Assert.Null(path.Patch);
        Assert.Null(path.Delete);
    }

    private static StubDocument CreateDocument(OperationDefinition operation)
    {
        return new()
        {
            OpenApi = "3.1.0",
            Paths = new(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = operation
                }
            }
        };
    }

    private static ResponseDefinition CreateResponse()
    {
        return new()
        {
            Content = new(StringComparer.Ordinal)
            {
                ["application/json"] = new()
                {
                    Example = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["message"] = "ok"
                    }
                }
            }
        };
    }

    private static QueryMatchResponseDefinition CreateMatchResponse()
    {
        return new()
        {
            StatusCode = 200,
            Content = new(StringComparer.Ordinal)
            {
                ["application/json"] = new()
                {
                    Example = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["message"] = "matched"
                    }
                }
            }
        };
    }
}
