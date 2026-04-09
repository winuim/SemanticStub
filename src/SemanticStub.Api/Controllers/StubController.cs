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
        var requestPath = NormalizeRequestPath(path);
        var dispatch = await DispatchRequestAsync(method, requestPath).ConfigureAwait(false);
        int? statusCode = null;

        try
        {
            return await CreateActionResultAsync(dispatch, requestPath, code => statusCode = code).ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();

            if (statusCode.HasValue)
            {
                RecordRequestObservation(dispatch, method, requestPath, statusCode.Value, stopwatch.Elapsed);
            }
        }
    }

    private static string NormalizeRequestPath(string? path)
    {
        return string.IsNullOrEmpty(path) ? "/" : "/" + path;
    }

    private async Task<StubDispatchResult> DispatchRequestAsync(string method, string requestPath)
    {
        var query = Request.Query.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var headers = Request.Headers.ToDictionary(entry => entry.Key, entry => entry.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        Request.EnableBuffering();
        var requestBody = await ReadRequestBodyAsync().ConfigureAwait(false);
        return await stubService.DispatchAsync(method, requestPath, query, headers, requestBody).ConfigureAwait(false);
    }

    private async Task<IActionResult> CreateActionResultAsync(StubDispatchResult dispatch, string requestPath, Action<int> setStatusCode)
    {
        if (dispatch.Result == StubMatchResult.Matched)
        {
            inspectionService.RecordLastMatchExplanation(dispatch.Explanation);
        }

        if (dispatch.Result == StubMatchResult.PathNotFound)
        {
            setStatusCode(StatusCodes.Status404NotFound);
            return NotFound();
        }

        if (dispatch.Result == StubMatchResult.MethodNotAllowed)
        {
            ApplyAllowHeader(requestPath);
            setStatusCode(StatusCodes.Status405MethodNotAllowed);
            return StatusCode(StatusCodes.Status405MethodNotAllowed);
        }

        if (dispatch.Result == StubMatchResult.ResponseNotConfigured || dispatch.Response is null)
        {
            setStatusCode(StatusCodes.Status500InternalServerError);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        var response = dispatch.Response;
        setStatusCode(response.StatusCode);

        if (response.DelayMilliseconds is > 0)
        {
            await Task.Delay(response.DelayMilliseconds.Value, HttpContext.RequestAborted).ConfigureAwait(false);
        }

        CopyResponseHeaders(response);

        if (!string.IsNullOrEmpty(response.FilePath))
        {
            Response.StatusCode = response.StatusCode;
            return PhysicalFile(response.FilePath, response.ContentType);
        }

        return new ContentResult
        {
            StatusCode = response.StatusCode,
            ContentType = response.ContentType,
            Content = response.Body
        };
    }

    private void ApplyAllowHeader(string requestPath)
    {
        var allowedMethods = stubService.GetAllowedMethods(requestPath);

        if (allowedMethods.Count > 0)
        {
            Response.Headers.Allow = string.Join(", ", allowedMethods);
        }
    }

    private void CopyResponseHeaders(StubResponse response)
    {
        foreach (var header in response.Headers)
        {
            if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Response.Headers[header.Key] = header.Value;
        }
    }

    private void RecordRequestObservation(StubDispatchResult dispatch, string method, string requestPath, int statusCode, TimeSpan elapsed)
    {
        inspectionService.RecordRequestMetrics(dispatch.Explanation, statusCode, elapsed);
        inspectionService.RecordRecentRequest(
            DateTimeOffset.UtcNow,
            method,
            requestPath,
            dispatch.Explanation,
            statusCode,
            elapsed);
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
