using Microsoft.AspNetCore.Mvc;
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

        if (!stubService.TryGetResponse(method, requestPath, out var response))
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
