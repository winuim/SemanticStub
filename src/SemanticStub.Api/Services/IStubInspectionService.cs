using SemanticStub.Api.Inspection;

namespace SemanticStub.Api.Services;

/// <summary>
/// Provides read-only runtime inspection of the active stub configuration.
/// </summary>
public interface IStubInspectionService
{
    /// <summary>
    /// Returns a point-in-time snapshot of the active configuration metadata.
    /// </summary>
    StubConfigSnapshot GetConfigSnapshot();

    /// <summary>
    /// Returns the list of all routes (path + method combinations) currently defined.
    /// </summary>
    IReadOnlyList<StubRouteInfo> GetRoutes();
}
