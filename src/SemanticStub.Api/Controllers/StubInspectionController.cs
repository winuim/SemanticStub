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
    private readonly IStubInspectionService _inspectionService;

    public StubInspectionController(IStubInspectionService inspectionService)
    {
        _inspectionService = inspectionService;
    }

    /// <summary>Returns a point-in-time snapshot of the active configuration metadata.</summary>
    [HttpGet("config")]
    public IActionResult GetConfig() => Ok(_inspectionService.GetConfigSnapshot());

    /// <summary>Returns the list of all routes currently defined in the loaded stub definitions.</summary>
    [HttpGet("routes")]
    public IActionResult GetRoutes() => Ok(_inspectionService.GetRoutes());

    /// <summary>Returns the effective runtime details for a single active route.</summary>
    [HttpGet("routes/{**routeId}")]
    public IActionResult GetRoute(string routeId)
    {
        var route = _inspectionService.GetRoute(routeId);
        return route is null
            ? NotFoundProblem("Route not found", $"Inspection route '{routeId}' was not found.")
            : Ok(route);
    }

    /// <summary>Returns the current runtime state for all configured scenarios.</summary>
    [HttpGet("scenarios")]
    public IActionResult GetScenarios() => Ok(_inspectionService.GetScenarioStates());

    /// <summary>Returns aggregate runtime metrics for real requests handled by the current process.</summary>
    [HttpGet("metrics")]
    public IActionResult GetMetrics() => Ok(_inspectionService.GetRuntimeMetrics());

    /// <summary>Resets aggregate runtime metrics and recent request history for the current process.</summary>
    [HttpPost("metrics/reset")]
    [HttpPost("metrics/resets")]
    public IActionResult ResetMetrics()
    {
        _inspectionService.ResetRuntimeMetrics();
        return NoContent();
    }

    /// <summary>Returns the bounded recent request history for real requests handled by the current process.</summary>
    [HttpGet("requests")]
    public IActionResult GetRecentRequests([FromQuery] int limit = 20) => Ok(_inspectionService.GetRecentRequests(limit));

    /// <summary>Exports a recorded real request as a runnable curl command.</summary>
    /// <param name="index">Zero-based index into the recent request history (0 = most recent).</param>
    [HttpGet("requests/{index:int}/export/curl")]
    public IActionResult ExportRequestAsCurl(int index)
    {
        if (index < 0)
        {
            return NotFoundProblem("Request not found", $"No recorded request at index {index}.");
        }

        var requests = _inspectionService.GetRecentRequests(index + 1);

        if (index >= requests.Count)
        {
            return NotFoundProblem("Request not found", $"No recorded request at index {index}.");
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        var curl = CurlExporter.Export(requests[index], baseUrl);
        return Content(curl, "text/plain");
    }

    /// <summary>Simulates how the runtime would match a virtual request without executing a response.</summary>
    [HttpPost("test-match")]
    public async Task<IActionResult> TestMatch([FromBody] MatchRequestInfo request)
    {
        return Ok(await _inspectionService.TestMatchAsync(request, HttpContext.RequestAborted));
    }

    /// <summary>Explains how the runtime evaluated a virtual request without executing a response.</summary>
    [HttpPost("explain")]
    public async Task<IActionResult> ExplainMatch([FromBody] MatchRequestInfo request)
    {
        return Ok(await _inspectionService.ExplainMatchAsync(request, HttpContext.RequestAborted));
    }

    /// <summary>Returns the explanation captured for the most recent real request.</summary>
    [HttpGet("explain/last")]
    public IActionResult ExplainLastMatch()
    {
        var explanation = _inspectionService.GetLastMatchExplanation();
        return explanation is null
            ? NotFoundProblem("Last match explanation not found", "No real request match explanation has been captured yet.")
            : Ok(explanation);
    }

    /// <summary>Resets all configured scenarios back to their initial state.</summary>
    [HttpPost("scenarios/reset")]
    [HttpPost("scenarios/resets")]
    public IActionResult ResetScenarios()
    {
        _inspectionService.ResetScenarioStates();
        return NoContent();
    }

    /// <summary>Resets a configured scenario back to its initial state.</summary>
    [HttpPost("scenarios/{name}/reset")]
    [HttpPost("scenarios/{name}/resets")]
    public IActionResult ResetScenario(string name)
    {
        return _inspectionService.ResetScenarioState(name)
            ? NoContent()
            : NotFoundProblem("Scenario not found", $"Scenario '{name}' was not found.");
    }

    private ObjectResult NotFoundProblem(string title, string detail)
    {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: title,
            detail: detail);
    }
}
