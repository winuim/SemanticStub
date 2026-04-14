using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class StubDefinitionLoaderTests
{
    [Fact]
    public void LoadDefaultDefinition_PreservesMatchedHeadersDuringNormalization()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /environment-users:
                get:
                  x-match:
                    - headers:
                        X-Env: staging
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              message: staging
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: default
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var operation = Assert.IsType<OperationDefinition>(document.Paths["/environment-users"].Get);
        var match = Assert.Single(operation.Matches);

        Assert.Equal("staging", match.Headers["X-Env"]);
    }

    [Fact]
    public void LoadDefaultDefinition_PreservesSemanticMatchDuringNormalization()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /search:
                post:
                  x-match:
                    - x-semantic-match: find admin users
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              message: admin
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var operation = Assert.IsType<OperationDefinition>(document.Paths["/search"].Post);
        var match = Assert.Single(operation.Matches);

        Assert.Equal("find admin users", match.SemanticMatch);
    }

    [Fact]
    public void LoadDefaultDefinition_PreservesResponseHeadersDuringNormalization()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /hello:
                get:
                  responses:
                    "200":
                      description: ok
                      headers:
                        X-Stub-Source:
                          description: Response source
                          example: loader
                        X-Trace-Id:
                          schema:
                            type: integer
                            example: 42
                      content:
                        application/json:
                          example:
                            message: hello
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var operation = Assert.IsType<OperationDefinition>(document.Paths["/hello"].Get);
        var response = operation.Responses["200"];

        Assert.Equal("loader", response.Headers["X-Stub-Source"].Example);
        Assert.Equal("42", Convert.ToString(response.Headers["X-Trace-Id"].Schema?.Example, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenOpenApiIsMissing()
    {
        using var workspace = TestWorkspace.Create(
            """
            paths:
              /hello:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: hello
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("The 'openapi' field is required.", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenPathsIsNull()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("At least one path must be configured under 'paths'.", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenPathHasNoSupportedOperation()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /hello: {}
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("Path '/hello' must define at least one supported operation.", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenOperationHasNoResponses()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /hello:
                get: {}
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("Path '/hello' GET must define at least one response or x-match entry.", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsForMissingResponseFile()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  responses:
                    "200":
                      description: ok
                      x-response-file: missing.json
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("references missing response file 'missing.json'", exception.Message);
        Assert.Contains("Path '/users' GET responses['200']", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsForEmptySemanticMatch()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /search:
                post:
                  x-match:
                    - x-semantic-match: "   "
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              message: admin
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0].x-semantic-match must not be empty", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_DoesNotReportCombinationErrorWhenSemanticMatchIsEmpty()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /search:
                post:
                  x-match:
                    - x-semantic-match: "   "
                      query:
                        role: admin
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              message: admin
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0].x-semantic-match must not be empty", exception.Message);
        Assert.DoesNotContain("x-match[0].x-semantic-match cannot be combined", exception.Message);
    }

    [Theory]
    [InlineData("query", "query:\n                        role: admin")]
    [InlineData("headers", "headers:\n                        X-Env: staging")]
    [InlineData("body", "body:\n                        role: admin")]
    public void LoadDefaultDefinition_ThrowsWhenSemanticMatchIsCombinedWithDeterministicCondition(
        string deterministicField,
        string deterministicCondition)
    {
        using var workspace = TestWorkspace.Create(
            $$"""
            openapi: 3.1.0
            paths:
              /search:
                post:
                  x-match:
                    - x-semantic-match: find admin users
                      {{deterministicCondition}}
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              message: admin
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains(
            $"x-match[0].x-semantic-match cannot be combined with {deterministicField}.",
            exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_DoesNotReportParameterDeclarationErrorsWhenSemanticMatchIsCombinedWithQuery()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /search:
                post:
                  parameters:
                    - name: status
                      in: query
                      schema:
                        type: string
                  x-match:
                    - x-semantic-match: find admin users
                      query:
                        role: admin
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              message: admin
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0].x-semantic-match cannot be combined with query.", exception.Message);
        Assert.DoesNotContain("x-match[0].query['role'] must reference a declared query parameter", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsForInvalidMatchedResponse()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  x-match:
                    - query:
                        role: admin
                      response:
                        statusCode: 0
                        content:
                          application/json:
                            example:
                              message: broken
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: default
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0] must define a positive statusCode", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsForOutOfRangeMatchedResponseStatusCode()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  x-match:
                    - query:
                        role: admin
                      response:
                        statusCode: 700
                        content:
                          application/json:
                            example:
                              message: broken
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: default
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0].response.statusCode must be between 100 and 599", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsForMalformedMatchedResponseWithoutResponse()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  x-match:
                    - query:
                        role: admin
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: default
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0] must define a positive statusCode", exception.Message);
        Assert.Contains("x-match[0].response must define content or 'x-response-file'", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsForMatchedResponseWithMissingResponseFile()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  x-match:
                    - query:
                        role: admin
                      response:
                        statusCode: 200
                        x-response-file: admin-users.json
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: default
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("references missing response file 'admin-users.json'", exception.Message);
        Assert.Contains("Path '/users' GET x-match[0].response", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsForUnsupportedResponseStatusKey()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /hello:
                get:
                  responses:
                    default:
                      description: ok
                      content:
                        application/json:
                          example:
                            message: hello
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("uses unsupported response key 'default'", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsForOutOfRangeResponseStatusKey()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  responses:
                    "700":
                      description: out-of-range
                      content:
                        application/json:
                          example:
                            message: broken
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("responses['700'] must use an HTTP status code between 100 and 599", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenJsonExampleIsMissing()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /hello:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json: {}
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("must define an example for 'application/json'", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_LoadsBodyMatchDefinitions()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /login:
                post:
                  x-match:
                    - body:
                        username: demo
                        password: secret
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              result: ok
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            result: fallback
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var match = Assert.Single(document.Paths["/login"].Post!.Matches);

        var body = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(match.Body);
        Assert.Equal("demo", body["username"]);
        Assert.Equal("secret", body["password"]);
    }

    [Fact]
    public void LoadDefaultDefinition_LoadsScenarioDefinitionFromResponseExtension()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /checkout:
                post:
                  responses:
                    "409":
                      description: pending
                      x-scenario:
                        name: checkout-flow
                        state: initial
                        next: confirmed
                      content:
                        application/json:
                          example:
                            result: pending
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var response = document.Paths["/checkout"].Post!.Responses["409"];

        var scenario = Assert.IsType<ScenarioDefinition>(response.Scenario);
        Assert.Equal("checkout-flow", scenario.Name);
        Assert.Equal("initial", scenario.State);
        Assert.Equal("confirmed", scenario.Next);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenScenarioNameIsMissing()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /checkout:
                post:
                  responses:
                    "409":
                      description: pending
                      x-scenario:
                        state: initial
                      content:
                        application/json:
                          example:
                            result: pending
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("responses['409'].x-scenario.name is required", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_AllowsMatchedQueryDefinedOnOperationParameters()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  parameters:
                    - name: role
                      in: query
                      schema:
                        type: string
                  x-match:
                    - query:
                        role: admin
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();

        Assert.Single(document.Paths["/users"].Get!.Matches);
    }

    [Fact]
    public void LoadDefaultDefinition_PreservesQueryParameterSchemaTypeAndTypedMatchValue()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  parameters:
                    - name: page
                      in: query
                      schema:
                        type: integer
                  x-match:
                    - query:
                        page: 1
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var operation = Assert.IsType<OperationDefinition>(document.Paths["/users"].Get);
        var parameter = Assert.Single(operation.Parameters);
        var match = Assert.Single(operation.Matches);

        Assert.Equal("integer", parameter.Schema?.Type);
        Assert.Equal("1", Assert.IsType<string>(match.Query["page"]));
    }

    [Fact]
    public void LoadDefaultDefinition_PreservesMultiValueQueryMatch()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /search:
                get:
                  parameters:
                    - name: tag
                      in: query
                      schema:
                        type: string
                  x-match:
                    - query:
                        tag:
                          - alpha
                          - beta
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              result: ordered
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            result: fallback
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var operation = Assert.IsType<OperationDefinition>(document.Paths["/search"].Get);
        var match = Assert.Single(operation.Matches);
        var values = Assert.IsAssignableFrom<IEnumerable<object?>>(match.Query["tag"]);

        Assert.Equal(["alpha", "beta"], values.Cast<string>().ToArray());
    }

    [Fact]
    public void LoadDefaultDefinition_PreservesQueryEqualsOperatorMatch()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  parameters:
                    - name: role
                      in: query
                      schema:
                        type: string
                  x-match:
                    - query:
                        role:
                          equals: admin
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var operation = Assert.IsType<OperationDefinition>(document.Paths["/users"].Get);
        var match = Assert.Single(operation.Matches);
        var role = Assert.IsType<Dictionary<string, object?>>(match.Query["role"]);

        Assert.Equal("admin", Assert.IsType<string>(role["equals"]));
    }

    [Fact]
    public void LoadDefaultDefinition_PreservesQueryRegexOperatorMatch()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  parameters:
                    - name: role
                      in: query
                      schema:
                        type: string
                  x-match:
                    - query:
                        role:
                          regex: ^admin-[0-9]+$
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var operation = Assert.IsType<OperationDefinition>(document.Paths["/users"].Get);
        var match = Assert.Single(operation.Matches);
        var role = Assert.IsType<Dictionary<string, object?>>(match.Query["role"]);

        Assert.Equal("^admin-[0-9]+$", Assert.IsType<string>(role["regex"]));
    }

    [Fact]
    public void LoadDefaultDefinition_AllowsMatchedQueryDefinedOnPathParameters()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                parameters:
                  - name: role
                    in: query
                    schema:
                      type: string
                get:
                  x-match:
                    - query:
                        role: admin
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();

        Assert.Single(document.Paths["/users"].Get!.Matches);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenMatchedQueryIsNotDeclared()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  parameters:
                    - name: status
                      in: query
                      schema:
                        type: string
                  x-match:
                    - query:
                        role: admin
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0].query['role'] must reference a declared query parameter", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenLegacyPartialQueryIsUsed()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  parameters:
                    - name: role
                      in: query
                      schema:
                        type: string
                  x-match:
                    - x-query-partial:
                        role: admin
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0].x-query-partial is no longer supported", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenLegacyRegexQueryIsUsed()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  parameters:
                    - name: role
                      in: query
                      schema:
                        type: string
                  x-match:
                    - x-query-regex:
                        role: ^admin-[0-9]+$
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0].x-query-regex is no longer supported", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenRegexOperatorPatternIsInvalid()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  parameters:
                    - name: role
                      in: query
                      schema:
                        type: string
                  x-match:
                    - query:
                        role:
                          regex: "^(admin$"
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0].query['role'].regex must be a valid regex pattern", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenOperatorValueIsAnEmptyMap()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  parameters:
                    - name: role
                      in: query
                      schema:
                        type: string
                  x-match:
                    - query:
                        role: {}
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0].query['role'] must define at least one supported operator", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenMatchOperatorIsUnsupported()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  parameters:
                    - name: role
                      in: query
                      schema:
                        type: string
                  x-match:
                    - query:
                        role:
                          contains: admin
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0].query['role'] uses unsupported operator 'contains'", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenMatchOperatorsAreMixed()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  parameters:
                    - name: role
                      in: query
                      schema:
                        type: string
                  x-match:
                    - query:
                        role:
                          equals: admin
                          regex: ^admin$
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0].query['role'] must not combine equals and regex operators", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_AllowsMatchedHeaderDefinedOnOperationParameters()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  parameters:
                    - name: X-Env
                      in: header
                      schema:
                        type: string
                  x-match:
                    - headers:
                        x-env: staging
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();

        Assert.Single(document.Paths["/users"].Get!.Matches);
    }

    [Fact]
    public void LoadDefaultDefinition_AllowsMatchedHeaderDefinedOnPathParameters()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                parameters:
                  - name: X-Env
                    in: header
                    schema:
                      type: string
                get:
                  x-match:
                    - headers:
                        X-Env: staging
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();

        Assert.Single(document.Paths["/users"].Get!.Matches);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenMatchedHeaderIsNotDeclared()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  parameters:
                    - name: X-Trace-Id
                      in: header
                      schema:
                        type: string
                  x-match:
                    - headers:
                        X-Env: staging
                      response:
                        statusCode: 200
                        content:
                          application/json:
                            example:
                              users: []
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            users: []
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("x-match[0].headers['X-Env'] must reference a declared header parameter", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_LoadsValidDefinition()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  responses:
                    "200":
                      description: ok
                      x-response-file: users.json
            """,
            sampleFiles:
            [
                ("users.json", "[{\"id\":1,\"name\":\"Alice\"}]")
            ]);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();

        Assert.Equal("3.1.0", document.OpenApi);
        Assert.True(document.Paths.ContainsKey("/users"));
    }

    [Fact]
    public void LoadDefaultDefinition_UsesConfiguredDefinitionsPath()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /configured:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: configured
            """,
            definitionsDirectoryName: "custom-stubs");

        var loader = new StubDefinitionLoader(
            workspace.Environment,
            Options.Create(new StubSettings
            {
                DefinitionsPath = "custom-stubs"
            }));

        var document = loader.LoadDefaultDefinition();

        Assert.True(document.Paths.ContainsKey("/configured"));
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenConfiguredAbsoluteDefinitionsPathDoesNotExist()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /configured:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: configured
            """);

        var missingPath = Path.Combine(workspace.RootPath, "missing-stubs");
        var loader = new StubDefinitionLoader(
            workspace.Environment,
            Options.Create(new StubSettings
            {
                DefinitionsPath = missingPath
            }));

        var exception = Assert.Throws<DirectoryNotFoundException>(() => loader.LoadDefaultDefinition());

        Assert.Contains(missingPath, exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenNoStubFilesAreFound()
    {
        using var workspace = TestWorkspace.CreateEmpty();
        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<FileNotFoundException>(() => loader.LoadDefaultDefinition());

        Assert.Equal("samples/basic-routing.yaml", exception.FileName);
    }

    [Fact]
    public void LoadDefaultDefinition_ResolvesResponseFileRelativeToDefinitionFile()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  responses:
                    "200":
                      description: ok
                      x-response-file: responses/users.json
            """,
            sampleFiles:
            [
                ("responses/users.json", "[{\"id\":1,\"name\":\"Alice\"}]")
            ]);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var responseFile = document.Paths["/users"].Get!.Responses["200"].ResponseFile;

        Assert.Equal(
            Path.Combine(workspace.RootPath, "samples", "responses", "users.json"),
            responseFile);
    }

    [Fact]
    public void LoadDefaultDefinition_ResolvesResponseFileRelativeToAdditionalDefinitionFile()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /status:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            status: base
            """,
            additionalStubFiles:
            [
                ("features/orders.stub.yaml",
                """
                openapi: 3.1.0
                paths:
                  /orders:
                    get:
                      responses:
                        "200":
                          description: ok
                          x-response-file: responses/orders.json
                """),
                ("features/responses/orders.json", "[{\"id\":10}]")
            ]);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var responseFile = document.Paths["/orders"].Get!.Responses["200"].ResponseFile;

        Assert.Equal(
            Path.Combine(workspace.RootPath, "samples", "features", "responses", "orders.json"),
            responseFile);
    }

    [Fact]
    public void LoadDefaultDefinition_LoadsAdditionalStubFiles()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            source: base
            """,
            additionalStubFiles:
            [
                ("orders.stub.yaml",
                """
                openapi: 3.1.0
                paths:
                  /orders:
                    get:
                      responses:
                        "200":
                          description: ok
                          content:
                            application/json:
                              example:
                                source: additional
                """)
            ]);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();

        Assert.Equal("3.1.0", document.OpenApi);
        Assert.True(document.Paths.ContainsKey("/users"));
        Assert.True(document.Paths.ContainsKey("/orders"));
    }

    [Fact]
    public void LoadDefaultDefinition_MergesDistinctMethodsAcrossStubFiles()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            method: get
            """,
            additionalStubFiles:
            [
                ("users.stub.yaml",
                """
                openapi: 3.1.0
                paths:
                  /users:
                    post:
                      responses:
                        "201":
                          description: created
                          content:
                            application/json:
                              example:
                                method: post
                """)
            ]);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();

        Assert.NotNull(document.Paths["/users"].Get);
        Assert.NotNull(document.Paths["/users"].Post);
    }

    [Fact]
    public void LoadDefaultDefinition_MergesIdenticalPathParametersAcrossStubFiles()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                parameters:
                  - name: role
                    in: query
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            method: get
            """,
            additionalStubFiles:
            [
                ("users.stub.yaml",
                """
                openapi: 3.1.0
                paths:
                  /users:
                    parameters:
                      - name: role
                        in: query
                    post:
                      responses:
                        "201":
                          description: created
                          content:
                            application/json:
                              example:
                                method: post
                """)
            ]);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var parameters = document.Paths["/users"].Parameters;

        var parameter = Assert.Single(parameters);
        Assert.Equal("role", parameter.Name);
        Assert.Equal("query", parameter.In);
        Assert.NotNull(document.Paths["/users"].Get);
        Assert.NotNull(document.Paths["/users"].Post);
    }

    [Fact]
    public void LoadDefaultDefinition_MergesDistinctPathParametersAcrossStubFiles()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                parameters:
                  - name: role
                    in: query
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            method: get
            """,
            additionalStubFiles:
            [
                ("users.stub.yaml",
                """
                openapi: 3.1.0
                paths:
                  /users:
                    parameters:
                      - name: X-Env
                        in: header
                    post:
                      responses:
                        "201":
                          description: created
                          content:
                            application/json:
                              example:
                                method: post
                """)
            ]);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var parameters = document.Paths["/users"].Parameters;

        Assert.Equal(2, parameters.Count);
        Assert.Contains(parameters, parameter => parameter.Name == "role" && parameter.In == "query");
        Assert.Contains(parameters, parameter => parameter.Name == "X-Env" && parameter.In == "header");
        Assert.NotNull(document.Paths["/users"].Get);
        Assert.NotNull(document.Paths["/users"].Post);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenStubFilesDefineSamePathAndMethod()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            source: base
            """,
            additionalStubFiles:
            [
                ("users.stub.yaml",
                """
                openapi: 3.1.0
                paths:
                  /users:
                    get:
                      responses:
                        "200":
                          description: ok
                          content:
                            application/json:
                              example:
                                source: duplicate
                """)
            ]);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("Path '/users' GET is defined in both 'basic-routing.yaml' and 'users.stub.yaml'.", exception.Message);
    }

    [Fact]
    public void LoadDefaultDefinition_ThrowsWhenStubFilesUseDifferentOpenApiVersions()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            source: base
            """,
            additionalStubFiles:
            [
                ("orders.stub.yaml",
                """
                openapi: 3.0.3
                paths:
                  /orders:
                    get:
                      responses:
                        "200":
                          description: ok
                          content:
                            application/json:
                              example:
                                source: additional
                """)
            ]);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("must use the same 'openapi' version", exception.Message);
    }

    [Fact]
    public void InterfaceContract_LoadsNormalizedDocumentAndResponseFileContent()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  responses:
                    "200":
                      description: ok
                      x-response-file: payloads/users.json
                      headers:
                        X-Stub-Source:
                          example: loader
                      content:
                        application/json:
                          schema:
                            type: object
            """,
            sampleFiles:
            [
                ("payloads/users.json", "{\"users\":[{\"id\":1,\"name\":\"Alice\"}]}")
            ]);

        IStubDefinitionLoader loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();
        var operation = Assert.IsType<OperationDefinition>(document.Paths["/users"].Get);
        var response = operation.Responses["200"];
        var body = loader.LoadResponseFileContent("payloads/users.json");

        Assert.Equal(
            Path.Combine(workspace.RootPath, "samples", "payloads", "users.json"),
            response.ResponseFile);
        Assert.Equal("loader", response.Headers["X-Stub-Source"].Example);
        Assert.Equal("{\"users\":[{\"id\":1,\"name\":\"Alice\"}]}", body);
    }

    [Fact]
    public void LoadResponseFileContent_ThrowsWhenRelativePathEscapesDefinitionsDirectory()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: ok
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadResponseFileContent("../../etc/passwd"));

        Assert.Contains("outside the definitions directory", exception.Message);
    }

    [Fact]
    public void LoadResponseFileContent_ThrowsWhenAbsolutePathEscapesDefinitionsDirectory()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: ok
            """);

        var loader = new StubDefinitionLoader(workspace.Environment);
        var outsidePath = Path.Combine(Path.GetTempPath(), $"semanticstub-outside-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(outsidePath, "{}");

            var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadResponseFileContent(outsidePath));

            Assert.Contains("outside the definitions directory", exception.Message);
        }
        finally
        {
            if (File.Exists(outsidePath))
            {
                File.Delete(outsidePath);
            }
        }
    }

    [Fact]
    public void LoadResponseFileContent_LoadsRelativePathWithinConfiguredDefinitionsDirectoryWithTrailingSeparator()
    {
        using var workspace = TestWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /users:
                get:
                  responses:
                    "200":
                      description: ok
                      x-response-file: payloads/users.json
            """,
            sampleFiles:
            [
                ("payloads/users.json", "{\"users\":[{\"id\":1}]}")
            ],
            definitionsDirectoryName: "custom-stubs");

        var loader = new StubDefinitionLoader(
            workspace.Environment,
            Options.Create(new StubSettings
            {
                DefinitionsPath = "custom-stubs/"
            }));

        var body = loader.LoadResponseFileContent("payloads/users.json");

        Assert.Equal("{\"users\":[{\"id\":1}]}", body);
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string rootPath)
        {
            RootPath = rootPath;
            Environment = new TestWebHostEnvironment
            {
                ContentRootPath = rootPath
            };
        }

        public string RootPath { get; }

        public IWebHostEnvironment Environment { get; }

        public static TestWorkspace Create(
            string yaml,
            (string FileName, string Content)[]? sampleFiles = null,
            (string FileName, string Content)[]? additionalStubFiles = null,
            string definitionsDirectoryName = "samples")
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "semanticstub-tests", Guid.NewGuid().ToString("N"));
            var samplesPath = Path.Combine(rootPath, definitionsDirectoryName);
            Directory.CreateDirectory(samplesPath);
            WriteFile(samplesPath, "basic-routing.yaml", yaml);

            foreach (var (fileName, content) in sampleFiles ?? [])
            {
                WriteFile(samplesPath, fileName, content);
            }

            foreach (var (fileName, content) in additionalStubFiles ?? [])
            {
                WriteFile(samplesPath, fileName, content);
            }

            return new TestWorkspace(rootPath);
        }

        public static TestWorkspace CreateEmpty(string definitionsDirectoryName = "samples")
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "semanticstub-tests", Guid.NewGuid().ToString("N"));
            var samplesPath = Path.Combine(rootPath, definitionsDirectoryName);
            Directory.CreateDirectory(samplesPath);

            return new TestWorkspace(rootPath);
        }

        private static void WriteFile(string rootPath, string relativePath, string content)
        {
            var fullPath = Path.Combine(rootPath, relativePath);
            var directoryPath = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(fullPath, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = null!;

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
