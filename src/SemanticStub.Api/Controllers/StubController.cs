using Microsoft.AspNetCore.Mvc;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;

namespace SemanticStub.Api.Controllers;

[ApiController]
[Route("{*path}")]
public sealed class StubController : ControllerBase
{
    private readonly StubService stubService;

    public StubController(StubService stubService)
    {
        this.stubService = stubService;
    }

    [HttpGet]
    public IActionResult Get(string? path)
    {
        return HandleRequest(HttpMethods.Get, path);
    }

    [HttpPost]
    public IActionResult Post(string? path)
    {
        return HandleRequest(HttpMethods.Post, path);
    }

    private IActionResult HandleRequest(string method, string? path)
    {
        var requestPath = string.IsNullOrEmpty(path) ? "/" : "/" + path;
        var matchResult = stubService.TryGetResponse(method, requestPath, out var response);

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

        return new ContentResult
        {
            StatusCode = response.StatusCode,
            ContentType = response.ContentType,
            Content = response.Body
        };
    }
}
