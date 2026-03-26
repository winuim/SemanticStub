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
        Assert.Contains("x-match[0].response must define 'application/json' content or 'x-response-file'", exception.Message);
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
    public void LoadDefaultDefinition_ThrowsWhenResponseFileContentTypeIsInvalid()
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
                      content:
                        text/plain: {}
            """,
            sampleFiles:
            [
                ("users.json", "[{\"id\":1,\"name\":\"Alice\"}]")
            ]);

        var loader = new StubDefinitionLoader(workspace.Environment);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadDefaultDefinition());

        Assert.Contains("must define 'application/json' content or 'x-response-file'", exception.Message);
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
