using Microsoft.AspNetCore.Mvc;
using SemanticStub.Api.Inspection;
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

    /// <summary>Returns the effective runtime details for a single active route.</summary>
    [HttpGet("routes/{**routeId}")]
    public IActionResult GetRoute(string routeId)
    {
        var route = inspectionService.GetRoute(routeId);
        return route is null ? NotFound() : Ok(route);
    }

    /// <summary>Returns the current runtime state for all configured scenarios.</summary>
    [HttpGet("scenarios")]
    public IActionResult GetScenarios() => Ok(inspectionService.GetScenarioStates());

    /// <summary>Returns aggregate runtime metrics for real requests handled by the current process.</summary>
    [HttpGet("metrics")]
    public IActionResult GetMetrics() => Ok(inspectionService.GetRuntimeMetrics());

    /// <summary>Returns the bounded recent request history for real requests handled by the current process.</summary>
    [HttpGet("requests")]
    public IActionResult GetRecentRequests([FromQuery] int limit = 20) => Ok(inspectionService.GetRecentRequests(limit));

    /// <summary>Simulates how the runtime would match a virtual request without executing a response.</summary>
    [HttpPost("test-match")]
    public async Task<IActionResult> TestMatch([FromBody] MatchRequestInfo request)
    {
        return Ok(await inspectionService.TestMatchAsync(request).ConfigureAwait(false));
    }

    /// <summary>Explains how the runtime evaluated a virtual request without executing a response.</summary>
    [HttpPost("explain")]
    public async Task<IActionResult> ExplainMatch([FromBody] MatchRequestInfo request)
    {
        return Ok(await inspectionService.ExplainMatchAsync(request).ConfigureAwait(false));
    }

    /// <summary>Returns the explanation captured for the most recent real request.</summary>
    [HttpGet("explain/last")]
    public IActionResult ExplainLastMatch()
    {
        var explanation = inspectionService.GetLastMatchExplanation();
        return explanation is null ? NotFound() : Ok(explanation);
    }

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
