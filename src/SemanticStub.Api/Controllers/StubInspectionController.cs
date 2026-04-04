using Microsoft.AspNetCore.Mvc;
using SemanticStub.Api.Services;

namespace SemanticStub.Api.Controllers;

/// <summary>
/// Exposes read-only runtime inspection endpoints for the active stub configuration.
/// </summary>
[ApiController]
[Route("api/stub-inspect")]
public sealed class StubInspectionController : ControllerBase
{
    private readonly IStubInspectionService inspectionService;

    public StubInspectionController(IStubInspectionService inspectionService)
    {
        this.inspectionService = inspectionService;
    }

    /// <summary>Returns a point-in-time snapshot of the active configuration metadata.</summary>
    [HttpGet("config")]
    public IActionResult GetConfig() => Ok(inspectionService.GetConfigSnapshot());

    /// <summary>Returns the list of all routes currently defined in the loaded stub definitions.</summary>
    [HttpGet("routes")]
    public IActionResult GetRoutes() => Ok(inspectionService.GetRoutes());
}
