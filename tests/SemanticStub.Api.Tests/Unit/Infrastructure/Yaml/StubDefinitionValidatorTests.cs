using SemanticStub.Application.Infrastructure.Yaml;
using SemanticStub.Infrastructure.Yaml;
using SemanticStub.Api.Models;
using SemanticStub.Application.Models;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Infrastructure.Yaml;

public sealed class StubDefinitionValidatorTests
{
    [Fact]
    public void ValidateDocument_AllowsValidResponseDefinition()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(responses: new()
            {
                ["200"] = CreateResponse()
            }));

        validator.ValidateDocument(document, Directory.GetCurrentDirectory());
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenOpenApiIsMissing()
    {
        var validator = new StubDefinitionValidator();
        var document = new StubDocument
        {
            OpenApi = string.Empty,
            Paths = new(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = CreateOperation(responses: new()
                    {
                        ["200"] = CreateResponse()
                    })
                }
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("The 'openapi' field is required.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenPathsIsEmpty()
    {
        var validator = new StubDefinitionValidator();
        var document = new StubDocument
        {
            OpenApi = "3.1.0"
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("At least one path must be configured under 'paths'.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenPathHasNoSupportedOperation()
    {
        var validator = new StubDefinitionValidator();
        var document = new StubDocument
        {
            OpenApi = "3.1.0",
            Paths = new(StringComparer.Ordinal)
            {
                ["/hello"] = new()
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' must define at least one supported operation.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenOperationHasNoResponsesOrMatches()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(new OperationDefinition());

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET must define at least one response or x-match entry.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsForUnsupportedResponseStatusKey()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(responses: new()
            {
                ["default"] = CreateResponse()
            }));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET responses['default'] uses unsupported response key 'default'.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsForOutOfRangeResponseStatusKey()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(responses: new()
            {
                ["700"] = CreateResponse()
            }));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET responses['700'] must use an HTTP status code between 100 and 599.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenResponseHasNoContentOrResponseFile()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(responses: new()
            {
                ["200"] = new ResponseDefinition()
            }));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET responses['200'] must define content or 'x-response-file'.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenResponseContentHasNoExample()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(responses: new()
            {
                ["200"] = new ResponseDefinition
                {
                    Content = new(StringComparer.Ordinal)
                    {
                        ["application/json"] = new()
                    }
                }
            }));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET responses['200'] must define an example for 'application/json'.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenResponseFileIsMissing()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(responses: new()
            {
                ["200"] = new ResponseDefinition
                {
                    ResponseFile = "missing.json"
                }
            }));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET responses['200'] references missing response file 'missing.json'.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenSemanticMatchIsEmpty()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(matches:
            [
                new()
                {
                    SemanticMatch = "   ",
                    Response = CreateMatchResponse()
                }
            ]));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET x-match[0].x-semantic-match must not be empty.", exception.Message);
    }

    [Theory]
    [MemberData(nameof(SemanticMatchDeterministicConditions))]
    public void ValidateDocument_ThrowsWhenSemanticMatchIsCombinedWithDeterministicCondition(
        QueryMatchDefinition match,
        string deterministicField)
    {
        var validator = new StubDefinitionValidator();
        match = WithResponse(match, CreateMatchResponse());
        var document = CreateDocument(
            CreateOperation(matches:
            [
                match
            ]));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains($"Path '/hello' GET x-match[0].x-semantic-match cannot be combined with {deterministicField}.", exception.Message);
        Assert.DoesNotContain("x-match[0].query['role'] must reference a declared query parameter", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenMatchedQueryIsNotDeclared()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(
                parameters:
                [
                    new()
                    {
                        Name = "status",
                        In = "query"
                    }
                ],
                matches:
                [
                    new()
                    {
                        Query = new(StringComparer.Ordinal)
                        {
                            ["role"] = "admin"
                        },
                        Response = CreateMatchResponse()
                    }
                ]));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET x-match[0].query['role'] must reference a declared query parameter.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_AllowsMatchedHeaderUsingCaseInsensitiveDeclaredParameter()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(
                parameters:
                [
                    new()
                    {
                        Name = "X-Env",
                        In = "header"
                    }
                ],
                matches:
                [
                    new()
                    {
                        Headers = new(StringComparer.OrdinalIgnoreCase)
                        {
                            ["x-env"] = "staging"
                        },
                        Response = CreateMatchResponse()
                    }
                ]));

        validator.ValidateDocument(document, Directory.GetCurrentDirectory());
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenMatchOperatorIsUnsupported()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(matches:
            [
                new()
                {
                    Query = new(StringComparer.Ordinal)
                    {
                        ["role"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["contains"] = "admin"
                        }
                    },
                    Response = CreateMatchResponse()
                }
            ]));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET x-match[0].query['role'] uses unsupported operator 'contains'.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenMatchOperatorsAreMixed()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(matches:
            [
                new()
                {
                    Query = new(StringComparer.Ordinal)
                    {
                        ["role"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["equals"] = "admin",
                            ["regex"] = "^admin$"
                        }
                    },
                    Response = CreateMatchResponse()
                }
            ]));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET x-match[0].query['role'] must not combine equals and regex operators.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenRegexPatternIsInvalid()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(matches:
            [
                new()
                {
                    Query = new(StringComparer.Ordinal)
                    {
                        ["role"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["regex"] = "["
                        }
                    },
                    Response = CreateMatchResponse()
                }
            ]));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET x-match[0].query['role'].regex must be a valid regex pattern.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenFormBodyIsCombinedWithJsonBody()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(matches:
            [
                new()
                {
                    Body = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["form"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["username"] = "demo"
                        },
                        ["json"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["username"] = "demo"
                        }
                    },
                    Response = CreateMatchResponse()
                }
            ]));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET x-match[0].body.form cannot be combined with body.json.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenFormBodyIsCombinedWithTextBody()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(matches:
            [
                new()
                {
                    Body = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["form"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["username"] = "demo"
                        },
                        ["text"] = "demo"
                    },
                    Response = CreateMatchResponse()
                }
            ]));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET x-match[0].body.form cannot be combined with body.text.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenFormBodyMatchOperatorIsUnsupported()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(matches:
            [
                new()
                {
                    Body = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["form"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["username"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["contains"] = "demo"
                            }
                        }
                    },
                    Response = CreateMatchResponse()
                }
            ]));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET x-match[0].body.form['username'] uses unsupported operator 'contains'.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenFormBodyMatchOperatorsAreMixed()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(matches:
            [
                new()
                {
                    Body = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["form"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["username"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["equals"] = "demo",
                                ["regex"] = "^demo$"
                            }
                        }
                    },
                    Response = CreateMatchResponse()
                }
            ]));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET x-match[0].body.form['username'] must not combine equals and regex operators.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenFormBodyRegexPatternIsInvalid()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(matches:
            [
                new()
                {
                    Body = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["form"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["username"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["regex"] = "["
                            }
                        }
                    },
                    Response = CreateMatchResponse()
                }
            ]));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET x-match[0].body.form['username'].regex must be a valid regex pattern.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenMatchedResponseStatusCodeIsNotPositive()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(matches:
            [
                new()
                {
                    Query = new(StringComparer.Ordinal)
                    {
                        ["role"] = "admin"
                    },
                    Response = CreateMatchResponse(statusCode: 0)
                }
            ]));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET x-match[0] must define a positive statusCode.", exception.Message);
    }

    [Fact]
    public void ValidateDocument_ThrowsWhenScenarioNameIsMissing()
    {
        var validator = new StubDefinitionValidator();
        var document = CreateDocument(
            CreateOperation(responses: new()
            {
                ["409"] = new ResponseDefinition
                {
                    Content = new(StringComparer.Ordinal)
                    {
                        ["application/json"] = new()
                        {
                            Example = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["message"] = "conflict"
                            }
                        }
                    },
                    Scenario = new()
                    {
                        State = "initial"
                    }
                }
            }));

        var exception = Assert.Throws<InvalidOperationException>(
            () => validator.ValidateDocument(document, Directory.GetCurrentDirectory()));

        Assert.Contains("Path '/hello' GET responses['409'].x-scenario.name is required.", exception.Message);
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

    private static OperationDefinition CreateOperation(
        List<ParameterDefinition>? parameters = null,
        Dictionary<string, ResponseDefinition>? responses = null,
        List<QueryMatchDefinition>? matches = null)
    {
        return new()
        {
            Parameters = parameters ?? [],
            Responses = responses ?? new(StringComparer.Ordinal),
            Matches = matches ?? []
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

    private static QueryMatchResponseDefinition CreateMatchResponse(int statusCode = 200)
    {
        return new()
        {
            StatusCode = statusCode,
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

    private static QueryMatchDefinition WithResponse(
        QueryMatchDefinition match,
        QueryMatchResponseDefinition response)
    {
        return new()
        {
            Query = match.Query,
            PartialQuery = match.PartialQuery,
            RegexQuery = match.RegexQuery,
            SemanticMatch = match.SemanticMatch,
            Headers = match.Headers,
            Body = match.Body,
            Response = response
        };
    }

    public static TheoryData<QueryMatchDefinition, string> SemanticMatchDeterministicConditions()
    {
        return new()
        {
            {
                new()
                {
                    SemanticMatch = "find users",
                    Query = new(StringComparer.Ordinal)
                    {
                        ["role"] = "admin"
                    }
                },
                "query"
            },
            {
                new()
                {
                    SemanticMatch = "find users",
                    PartialQuery = new(StringComparer.Ordinal)
                    {
                        ["role"] = "admin"
                    }
                },
                "x-query-partial"
            },
            {
                new()
                {
                    SemanticMatch = "find users",
                    RegexQuery = new(StringComparer.Ordinal)
                    {
                        ["role"] = "^admin$"
                    }
                },
                "x-query-regex"
            },
            {
                new()
                {
                    SemanticMatch = "find users",
                    Headers = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["X-Env"] = "staging"
                    }
                },
                "headers"
            },
            {
                new()
                {
                    SemanticMatch = "find users",
                    Body = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["json"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["role"] = "admin"
                        }
                    }
                },
                "body"
            }
        };
    }
}
