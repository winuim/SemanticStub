using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Controllers;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class StubControllerTests
{
    [Fact]
    public async Task Post_DelegatesRequestToIStubService_AndReturnsMatchedContentResponse()
    {
        var stubService = new RecordingStubService(
            StubMatchResult.Matched,
            new StubResponse
            {
                StatusCode = StatusCodes.Status201Created,
                ContentType = "application/json",
                Body = "{\"message\":\"created\"}",
                Headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
                {
                    ["X-Stub-Source"] = "controller-test"
                }
            });

        var controller = CreateController(stubService);
        controller.Request.Method = HttpMethods.Post;
        controller.Request.QueryString = new QueryString("?role=admin");
        controller.Request.Headers["X-Env"] = "staging";
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"username\":\"demo\"}"));

        var result = await controller.Post("users");

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status201Created, contentResult.StatusCode);
        Assert.Equal("application/json", contentResult.ContentType);
        Assert.Equal("{\"message\":\"created\"}", contentResult.Content);
        Assert.Equal("controller-test", controller.Response.Headers["X-Stub-Source"]);

        Assert.Equal(HttpMethods.Post, stubService.Method);
        Assert.Equal("/users", stubService.Path);
        Assert.Equal("admin", Assert.Single(stubService.Query["role"]));
        Assert.Equal("staging", stubService.Headers["X-Env"]);
        Assert.Equal("{\"username\":\"demo\"}", stubService.Body);
        Assert.NotNull(controllerInspectionService.LastRecordedExplanation);
        Assert.Equal("Matched", controllerInspectionService.LastRecordedExplanation!.Result.MatchResult);
        Assert.Equal(HttpMethods.Post, controllerInspectionService.LastRecordedExplanation.Result.Method);
        Assert.Equal("/users", controllerInspectionService.LastRecordedExplanation.Result.PathPattern);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenPathNotFound()
    {
        var stubService = new RecordingStubService(StubMatchResult.PathNotFound, new StubResponse());
        var controller = CreateController(stubService);

        var result = await controller.Get("unknown/path");

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(HttpMethods.Get, stubService.Method);
        Assert.Equal("/unknown/path", stubService.Path);
    }

    [Fact]
    public async Task Delete_ReturnsMethodNotAllowed_WhenMethodNotAllowed()
    {
        var stubService = new RecordingStubService(
            StubMatchResult.MethodNotAllowed,
            new StubResponse(),
            allowedMethods: [HttpMethods.Get, HttpMethods.Post]);
        var controller = CreateController(stubService);

        var result = await controller.Delete("users/1");

        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status405MethodNotAllowed, statusResult.StatusCode);
        Assert.Equal(HttpMethods.Delete, stubService.Method);
        Assert.Equal("GET, POST", controller.Response.Headers.Allow.ToString());
    }

    [Fact]
    public async Task Get_ReturnsInternalServerError_WhenResponseNotConfigured()
    {
        var stubService = new RecordingStubService(StubMatchResult.ResponseNotConfigured, new StubResponse());
        var controller = CreateController(stubService);

        var result = await controller.Get("orders");

        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task Put_DelegatesRequestWithPutMethod()
    {
        var stubService = new RecordingStubService(StubMatchResult.Matched, new StubResponse { StatusCode = 200 });
        var controller = CreateController(stubService);

        await controller.Put("items/42");

        Assert.Equal(HttpMethods.Put, stubService.Method);
        Assert.Equal("/items/42", stubService.Path);
    }

    [Fact]
    public async Task Patch_DelegatesRequestWithPatchMethod()
    {
        var stubService = new RecordingStubService(StubMatchResult.Matched, new StubResponse { StatusCode = 200 });
        var controller = CreateController(stubService);

        await controller.Patch("items/42");

        Assert.Equal(HttpMethods.Patch, stubService.Method);
        Assert.Equal("/items/42", stubService.Path);
    }

    [Fact]
    public async Task Delete_DelegatesRequestWithDeleteMethod()
    {
        var stubService = new RecordingStubService(StubMatchResult.Matched, new StubResponse { StatusCode = 204 });
        var controller = CreateController(stubService);

        await controller.Delete("items/42");

        Assert.Equal(HttpMethods.Delete, stubService.Method);
        Assert.Equal("/items/42", stubService.Path);
    }

    [Fact]
    public async Task Get_NormalizesNullPath_ToRootPath()
    {
        var stubService = new RecordingStubService(StubMatchResult.Matched, new StubResponse { StatusCode = 200 });
        var controller = CreateController(stubService);

        await controller.Get(null);

        Assert.Equal("/", stubService.Path);
    }

    [Fact]
    public async Task Post_PassesNullBody_WhenBodyIsWhitespace()
    {
        var stubService = new RecordingStubService(StubMatchResult.Matched, new StubResponse { StatusCode = 200 });
        var controller = CreateController(stubService);
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("   "));

        await controller.Post("echo");

        Assert.Null(stubService.Body);
    }

    [Fact]
    public async Task Post_TreatsUnreadableBodyAsNull_InsteadOfThrowing()
    {
        var stubService = new RecordingStubService(StubMatchResult.Matched, new StubResponse { StatusCode = 200 });
        var controller = CreateController(stubService);
        controller.Request.Body = new ThrowingReadStream();

        var result = await controller.Post("echo");

        Assert.IsType<ContentResult>(result);
        Assert.Null(stubService.Body);
    }

    [Fact]
    public async Task Get_ReturnsPhysicalFileResult_WhenResponseHasFilePath()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "file content");
            var stubService = new RecordingStubService(
                StubMatchResult.Matched,
                new StubResponse
                {
                    StatusCode = StatusCodes.Status200OK,
                    ContentType = "application/octet-stream",
                    FilePath = tempFile
                });
            var controller = CreateController(stubService);

            var result = await controller.Get("files/data");

            var fileResult = Assert.IsType<PhysicalFileResult>(result);
            Assert.Equal(StatusCodes.Status200OK, controller.Response.StatusCode);
            Assert.Equal(tempFile, fileResult.FileName);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Post_SkipsContentTypeHeader_WhenCopyingResponseHeaders()
    {
        var stubService = new RecordingStubService(
            StubMatchResult.Matched,
            new StubResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = "{}",
                Headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Content-Type"] = "text/html",
                    ["X-Custom"] = "value"
                }
            });
        var controller = CreateController(stubService);

        var result = await controller.Post("data");

        var contentResult = Assert.IsType<ContentResult>(result);
        // Content-Type from StubResponse.ContentType, not from Headers
        Assert.Equal("application/json", contentResult.ContentType);
        Assert.False(controller.Response.Headers.ContainsKey("Content-Type"));
        Assert.Equal("value", controller.Response.Headers["X-Custom"].ToString());
    }

    [Fact]
    public async Task Get_RecordsLastMatchRequestForInspection()
    {
        var stubService = new RecordingStubService(StubMatchResult.Matched, new StubResponse { StatusCode = 200 });
        var inspectionService = new RecordingInspectionService();
        var controller = CreateController(stubService, inspectionService);
        controller.Request.QueryString = new QueryString("?role=admin&role=owner");
        controller.Request.Headers["X-Env"] = "staging";

        await controller.Get("users");

        var recorded = Assert.IsType<MatchExplanationInfo>(inspectionService.LastRecordedExplanation);
        Assert.Equal("Matched", recorded.Result.MatchResult);
        Assert.Equal("/users", recorded.Result.PathPattern);
        Assert.NotEmpty(recorded.Result.Candidates);
    }

    [Fact]
    public async Task Get_DoesNotOverwriteLastMatchExplanation_WhenRequestDoesNotMatch()
    {
        var stubService = new RecordingStubService(StubMatchResult.PathNotFound, new StubResponse());
        var inspectionService = new RecordingInspectionService
        {
            LastRecordedExplanation = new MatchExplanationInfo
            {
                Result = new MatchSimulationInfo
                {
                    MatchResult = "Matched",
                    PathPattern = "/users"
                }
            }
        };
        var controller = CreateController(stubService, inspectionService);

        await controller.Get("unknown");

        Assert.NotNull(inspectionService.LastRecordedExplanation);
        Assert.Equal("/users", inspectionService.LastRecordedExplanation!.Result.PathPattern);
    }

    private static readonly RecordingInspectionService controllerInspectionService = new();

    private static StubController CreateController(IStubService stubService, IStubInspectionService? inspectionService = null)
    {
        controllerInspectionService.LastRecordedExplanation = null;

        return new StubController(stubService, inspectionService ?? controllerInspectionService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private sealed class RecordingInspectionService : IStubInspectionService
    {
        public MatchExplanationInfo? LastRecordedExplanation { get; set; }

        public StubConfigSnapshot GetConfigSnapshot() => throw new NotSupportedException();

        public IReadOnlyList<StubRouteInfo> GetRoutes() => throw new NotSupportedException();

        public StubRouteDetailInfo? GetRoute(string routeId) => throw new NotSupportedException();

        public IReadOnlyList<ScenarioStateInfo> GetScenarioStates() => throw new NotSupportedException();

        public Task<MatchSimulationInfo> TestMatchAsync(MatchRequestInfo request) => throw new NotSupportedException();

        public Task<MatchExplanationInfo> ExplainMatchAsync(MatchRequestInfo request) => throw new NotSupportedException();

        public MatchExplanationInfo? GetLastMatchExplanation() => throw new NotSupportedException();

        public void RecordLastMatchExplanation(MatchExplanationInfo explanation)
        {
            LastRecordedExplanation = explanation;
        }

        public void ResetScenarioStates() => throw new NotSupportedException();

        public bool ResetScenarioState(string scenarioName) => throw new NotSupportedException();
    }

    private sealed class RecordingStubService : IStubService
    {
        private readonly StubMatchResult matchResult;
        private readonly StubResponse response;

        public RecordingStubService(StubMatchResult matchResult, StubResponse response, IReadOnlyList<string>? allowedMethods = null)
        {
            this.matchResult = matchResult;
            this.response = response;
            AllowedMethods = allowedMethods ?? Array.Empty<string>();
        }

        public string Method { get; private set; } = string.Empty;

        public string Path { get; private set; } = string.Empty;

        public IReadOnlyDictionary<string, StringValues> Query { get; private set; } =
            new Dictionary<string, StringValues>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, string> Headers { get; private set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string? Body { get; private set; }

        public IReadOnlyList<string> AllowedMethods { get; }

        public IReadOnlyList<string> GetAllowedMethods(string path)
        {
            return AllowedMethods;
        }

        public Task<StubDispatchResult> DispatchAsync(
            string method,
            string path,
            IReadOnlyDictionary<string, StringValues> query,
            IReadOnlyDictionary<string, string> headers,
            string? body)
        {
            Method = method;
            Path = path;
            Query = query;
            Headers = headers;
            Body = body;

            return Task.FromResult(new StubDispatchResult
            {
                Result = matchResult,
                Response = matchResult == StubMatchResult.Matched ? response : null,
                Explanation = new MatchExplanationInfo
                {
                    PathMatched = true,
                    MethodMatched = true,
                    DeterministicCandidates =
                    [
                        new MatchCandidateInfo
                        {
                            CandidateIndex = 0,
                            Matched = matchResult == StubMatchResult.Matched
                        }
                    ],
                    Result = new MatchSimulationInfo
                    {
                        Matched = matchResult == StubMatchResult.Matched,
                        MatchResult = matchResult.ToString(),
                        Method = method,
                        PathPattern = path,
                        Candidates =
                        [
                            new MatchCandidateInfo
                            {
                                CandidateIndex = 0,
                                Matched = matchResult == StubMatchResult.Matched
                            }
                        ]
                    }
                }
            });
        }

        public Task<MatchExplanationInfo> ExplainMatchAsync(MatchRequestInfo request)
        {
            return Task.FromResult(new MatchExplanationInfo
            {
                Result = new MatchSimulationInfo
                {
                    Matched = matchResult == StubMatchResult.Matched,
                    MatchResult = matchResult.ToString(),
                    Method = request.Method,
                    PathPattern = request.Path,
                }
            });
        }

        public StubMatchResult TryGetResponse(
            string method,
            string path,
            IReadOnlyDictionary<string, StringValues> query,
            IReadOnlyDictionary<string, string> headers,
            string? body,
            out StubResponse? response)
        {
            Method = method;
            Path = path;
            Query = query;
            Headers = headers;
            Body = body;

            if (matchResult == StubMatchResult.Matched)
            {
                response = this.response;
            }
            else
            {
                response = null!;
            }

            return matchResult;
        }
    }

    private sealed class ThrowingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Simulated disconnect.");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("Simulated disconnect."));

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
