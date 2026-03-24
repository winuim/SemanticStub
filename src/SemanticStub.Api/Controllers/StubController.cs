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
        var requestPath = string.IsNullOrEmpty(path) ? "/" : "/" + path;

        if (!stubService.TryGetResponse(HttpMethods.Get, requestPath, out var response))
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
