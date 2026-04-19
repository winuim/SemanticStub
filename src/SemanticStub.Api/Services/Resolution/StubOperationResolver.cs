using SemanticStub.Api.Models;
using SemanticStub.Application.Models;

namespace SemanticStub.Api.Services;

internal static class StubOperationResolver
{
    public static OperationDefinition? GetOperation(string method, PathItemDefinition pathItem)
    {
        if (HttpMethods.IsGet(method))
        {
            return pathItem.Get;
        }

        if (HttpMethods.IsPost(method))
        {
            return pathItem.Post;
        }

        if (HttpMethods.IsPut(method))
        {
            return pathItem.Put;
        }

        if (HttpMethods.IsPatch(method))
        {
            return pathItem.Patch;
        }

        if (HttpMethods.IsDelete(method))
        {
            return pathItem.Delete;
        }

        return null;
    }

    public static bool TryResolveOperation(
        StubDocument document,
        string method,
        string path,
        out string pathPattern,
        out PathItemDefinition pathItem,
        out OperationDefinition operation,
        out StubMatchResult failedMatchResult)
    {
        pathPattern = string.Empty;
        pathItem = null!;
        operation = null!;
        failedMatchResult = StubMatchResult.Matched;

        var resolvedPath = StubRouteResolver.ResolvePath(document, path);

        if (resolvedPath is null)
        {
            failedMatchResult = StubMatchResult.PathNotFound;
            return false;
        }

        var (resolvedPathPattern, resolvedPathItem) = resolvedPath.Value;
        var resolvedOperation = GetOperation(method, resolvedPathItem);

        if (resolvedOperation is null)
        {
            failedMatchResult = StubMatchResult.MethodNotAllowed;
            pathPattern = resolvedPathPattern;
            pathItem = resolvedPathItem;
            return false;
        }

        pathPattern = resolvedPathPattern;
        pathItem = resolvedPathItem;
        operation = resolvedOperation;
        return true;
    }
}
