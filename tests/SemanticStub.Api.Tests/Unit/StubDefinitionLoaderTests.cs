using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SemanticStub.Api.Infrastructure.Yaml;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class StubDefinitionLoaderTests
{
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
            ("users.json", "[{\"id\":1,\"name\":\"Alice\"}]"));

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
            ("users.json", "[{\"id\":1,\"name\":\"Alice\"}]"));

        var loader = new StubDefinitionLoader(workspace.Environment);

        var document = loader.LoadDefaultDefinition();

        Assert.Equal("3.1.0", document.OpenApi);
        Assert.True(document.Paths.ContainsKey("/users"));
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

        public static TestWorkspace Create(string yaml, params (string FileName, string Content)[] sampleFiles)
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "semanticstub-tests", Guid.NewGuid().ToString("N"));
            var samplesPath = Path.Combine(rootPath, "samples");
            Directory.CreateDirectory(samplesPath);
            File.WriteAllText(Path.Combine(samplesPath, "basic-routing.yaml"), yaml);

            foreach (var (fileName, content) in sampleFiles)
            {
                File.WriteAllText(Path.Combine(samplesPath, fileName), content);
            }

            return new TestWorkspace(rootPath);
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
