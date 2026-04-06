using Microsoft.AspNetCore.Mvc;
using SemanticStub.Api.Services;

namespace SemanticStub.Api.Controllers;

/// <summary>
/// Exposes read-only runtime inspection endpoints for the active stub configuration.
/// </summary>
/// <remarks>
/// The route prefix <c>_semanticstub/runtime</c> is reserved for the inspection feature.
/// YAML stub definitions that use paths under <c>/_semanticstub/runtime</c> will be
/// shadowed by these endpoints and will not be reachable at runtime.
/// </remarks>
[ApiController]
[Route("_semanticstub/runtime")]
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

    /// <summary>Returns the current runtime state for all configured scenarios.</summary>
    [HttpGet("scenarios")]
    public IActionResult GetScenarios() => Ok(inspectionService.GetScenarioStates());

    /// <summary>Resets all configured scenarios back to their initial state.</summary>
    [HttpPost("scenarios/reset")]
    public IActionResult ResetScenarios()
    {
        inspectionService.ResetScenarioStates();
        return NoContent();
    }

    /// <summary>Resets a configured scenario back to its initial state.</summary>
    [HttpPost("scenarios/{name}/reset")]
    public IActionResult ResetScenario(string name)
    {
        return inspectionService.ResetScenarioState(name)
            ? NoContent()
            : NotFound();
    }
}
