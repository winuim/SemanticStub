using Microsoft.AspNetCore.Mvc;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;

namespace SemanticStub.Api.Controllers;

/// <summary>
/// Routes every incoming request through the stub engine so mocked behavior stays defined in YAML rather than duplicated across controllers.
/// </summary>
[ApiController]
[Route("{*path}")]
public sealed class StubController : ControllerBase
{
    private readonly StubService stubService;

    public StubController(StubService stubService)
    {
        this.stubService = stubService;
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
        var requestPath = string.IsNullOrEmpty(path) ? "/" : "/" + path;
        var query = Request.Query.ToDictionary(entry => entry.Key, entry => entry.Value.ToString(), StringComparer.Ordinal);
        var headers = Request.Headers.ToDictionary(entry => entry.Key, entry => entry.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var requestBody = await ReadRequestBodyAsync();
        var matchResult = stubService.TryGetResponse(method, requestPath, query, headers, requestBody, out var response);

        if (matchResult == StubMatchResult.PathNotFound)
        {
            return NotFound();
        }

        if (matchResult == StubMatchResult.MethodNotAllowed)
        {
            return StatusCode(StatusCodes.Status405MethodNotAllowed);
        }

        if (matchResult != StubMatchResult.Matched)
        {
            return NotFound();
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

        return new ContentResult
        {
            StatusCode = response.StatusCode,
            ContentType = response.ContentType,
            Content = response.Body
        };
    }

    private async Task<string?> ReadRequestBodyAsync()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        return string.IsNullOrWhiteSpace(body) ? null : body;
    }
}
