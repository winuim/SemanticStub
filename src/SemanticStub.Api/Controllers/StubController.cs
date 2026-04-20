using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
    private readonly IStubService _stubService;
    private readonly IStubInspectionService _inspectionService;
    private readonly ILogger<StubController> _logger;

    public StubController(IStubService stubService, IStubInspectionService inspectionService, ILogger<StubController> logger)
    {
        _stubService = stubService;
        _inspectionService = inspectionService;
        _logger = logger;
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
        var dispatch = await DispatchRequestAsync(method, requestPath);
        int? statusCode = null;

        try
        {
            return await CreateActionResultAsync(dispatch, requestPath, code => statusCode = code);
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
        var requestBody = await StubRequestBodyReader.ReadAsync(Request, _logger);
        return await _stubService.DispatchAsync(method, requestPath, query, headers, requestBody, HttpContext.RequestAborted);
    }

    private async Task<IActionResult> CreateActionResultAsync(StubDispatchResult dispatch, string requestPath, Action<int> setStatusCode)
    {
        if (dispatch.Result == StubMatchResult.Matched)
        {
            _inspectionService.RecordLastMatchExplanation(dispatch.Explanation);
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
            await Task.Delay(response.DelayMilliseconds.Value, HttpContext.RequestAborted);
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
        var allowedMethods = _stubService.GetAllowedMethods(requestPath);

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
        _inspectionService.RecordRequestMetrics(dispatch.Explanation, statusCode, elapsed);
        _inspectionService.RecordRecentRequest(
            DateTimeOffset.UtcNow,
            method,
            requestPath,
            dispatch.Explanation,
            statusCode,
            elapsed);
    }

}
