using Microsoft.AspNetCore.Mvc;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using System.Diagnostics;

namespace SemanticStub.Api.Controllers;

/// <summary>
/// Routes every incoming request through the stub engine so mocked behavior stays defined in YAML rather than duplicated across controllers.
/// </summary>
[ApiController]
[Route("{*path}")]
public sealed class StubController : ControllerBase
{
    private readonly IStubService stubService;
    private readonly IStubInspectionService inspectionService;

    public StubController(IStubService stubService, IStubInspectionService inspectionService)
    {
        this.stubService = stubService;
        this.inspectionService = inspectionService;
    }

    /// <summary>
    /// Handles GET requests through the shared stub resolution path so verb-specific endpoints do not drift from YAML definitions.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> Get(string? path)
    {
        return HandleRequest(HttpMethods.Get, path);
    }

    /// <summary>
    /// Handles POST requests through the shared stub resolution path so verb-specific endpoints do not drift from YAML definitions.
    /// </summary>
    [HttpPost]
    public Task<IActionResult> Post(string? path)
    {
        return HandleRequest(HttpMethods.Post, path);
    }

    /// <summary>
    /// Handles PUT requests through the shared stub resolution path so verb-specific endpoints do not drift from YAML definitions.
    /// </summary>
    [HttpPut]
    public Task<IActionResult> Put(string? path)
    {
        return HandleRequest(HttpMethods.Put, path);
    }

    /// <summary>
    /// Handles PATCH requests through the shared stub resolution path so verb-specific endpoints do not drift from YAML definitions.
    /// </summary>
    [HttpPatch]
    public Task<IActionResult> Patch(string? path)
    {
        return HandleRequest(HttpMethods.Patch, path);
    }

    /// <summary>
    /// Handles DELETE requests through the shared stub resolution path so verb-specific endpoints do not drift from YAML definitions.
    /// </summary>
    [HttpDelete]
    public Task<IActionResult> Delete(string? path)
    {
        return HandleRequest(HttpMethods.Delete, path);
    }

    private async Task<IActionResult> HandleRequest(string method, string? path)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestPath = string.IsNullOrEmpty(path) ? "/" : "/" + path;
        var query = Request.Query.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var headers = Request.Headers.ToDictionary(entry => entry.Key, entry => entry.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        Request.EnableBuffering();
        var requestBody = await ReadRequestBodyAsync();
        var dispatch = await stubService.DispatchAsync(method, requestPath, query, headers, requestBody).ConfigureAwait(false);
        var matchResult = dispatch.Result;
        var response = dispatch.Response;

        if (matchResult == StubMatchResult.Matched)
        {
            inspectionService.RecordLastMatchExplanation(dispatch.Explanation);
        }

        if (matchResult == StubMatchResult.PathNotFound)
        {
            return CompleteRequest(NotFound(), StatusCodes.Status404NotFound);
        }

        if (matchResult == StubMatchResult.MethodNotAllowed)
        {
            var allowedMethods = stubService.GetAllowedMethods(requestPath);

            if (allowedMethods.Count > 0)
            {
                Response.Headers.Allow = string.Join(", ", allowedMethods);
            }

            return CompleteRequest(
                StatusCode(StatusCodes.Status405MethodNotAllowed),
                StatusCodes.Status405MethodNotAllowed);
        }

        if (matchResult == StubMatchResult.ResponseNotConfigured)
        {
            return CompleteRequest(
                StatusCode(StatusCodes.Status500InternalServerError),
                StatusCodes.Status500InternalServerError);
        }

        if (response is null)
        {
            return CompleteRequest(
                StatusCode(StatusCodes.Status500InternalServerError),
                StatusCodes.Status500InternalServerError);
        }

        if (response.DelayMilliseconds is > 0)
        {
            await Task.Delay(response.DelayMilliseconds.Value, HttpContext.RequestAborted);
        }

        foreach (var header in response.Headers)
        {
            if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Response.Headers[header.Key] = header.Value;
        }

        if (!string.IsNullOrEmpty(response.FilePath))
        {
            Response.StatusCode = response.StatusCode;
            return CompleteRequest(PhysicalFile(response.FilePath, response.ContentType), response.StatusCode);
        }

        return CompleteRequest(new ContentResult
        {
            StatusCode = response.StatusCode,
            ContentType = response.ContentType,
            Content = response.Body
        }, response.StatusCode);

        IActionResult CompleteRequest(IActionResult result, int statusCode)
        {
            stopwatch.Stop();
            inspectionService.RecordRequestMetrics(dispatch.Explanation, statusCode, stopwatch.Elapsed);
            return result;
        }
    }

    private async Task<string?> ReadRequestBodyAsync()
    {
        try
        {
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();

            if (Request.Body.CanSeek)
            {
                Request.Body.Position = 0;
            }

            return string.IsNullOrWhiteSpace(body) ? null : body;
        }
        catch (IOException)
        {
            return null;
        }
        catch (OperationCanceledException) when (Request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return null;
        }
    }
}
