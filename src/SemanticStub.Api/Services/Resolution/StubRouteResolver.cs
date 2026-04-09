using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

internal static class StubRouteResolver
{
    public static string NormalizeMethod(string method)
        => string.IsNullOrWhiteSpace(method) ? HttpMethods.Get : method.Trim().ToUpperInvariant();

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        return path.StartsWith('/')
            ? path
            : "/" + path;
    }

    public static PathItemDefinition? ResolvePathItem(StubDocument document, string requestPath)
    {
        return ResolvePath(document, requestPath)?.PathItem;
    }

    public static (string PathPattern, PathItemDefinition PathItem)? ResolvePath(StubDocument document, string requestPath)
    {
        if (document.Paths.TryGetValue(requestPath, out var exactPathItem))
        {
            return (requestPath, exactPathItem);
        }

        return document.Paths
            .Where(entry => IsTemplateMatch(entry.Key, requestPath))
            .OrderByDescending(entry => GetTemplateSpecificity(entry.Key))
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => ((string PathPattern, PathItemDefinition PathItem)?)(entry.Key, entry.Value))
            .FirstOrDefault();
    }

    private static bool IsTemplateMatch(string templatePath, string requestPath)
    {
        var templateSegments = GetPathSegments(templatePath);
        var requestSegments = GetPathSegments(requestPath);

        if (templateSegments.Length != requestSegments.Length)
        {
            return false;
        }

        for (var index = 0; index < templateSegments.Length; index++)
        {
            var templateSegment = templateSegments[index];
            var requestSegment = requestSegments[index];

            if (IsPathParameterSegment(templateSegment))
            {
                if (string.IsNullOrEmpty(requestSegment))
                {
                    return false;
                }

                continue;
            }

            if (!string.Equals(templateSegment, requestSegment, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static int GetTemplateSpecificity(string templatePath)
    {
        return GetPathSegments(templatePath).Count(segment => !IsPathParameterSegment(segment));
    }

    private static string[] GetPathSegments(string path)
    {
        return path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool IsPathParameterSegment(string segment)
    {
        return segment.Length > 2 &&
               segment[0] == '{' &&
               segment[^1] == '}' &&
               !segment[1..^1].Contains('{') &&
               !segment[1..^1].Contains('}');
    }
}
