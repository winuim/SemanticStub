using Microsoft.AspNetCore.Http;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class StubRouteResolverTests
{
    [Fact]
    public void ResolvePath_PrefersExactPathOverTemplatePath()
    {
        var exactPathItem = new PathItemDefinition();
        var templatePathItem = new PathItemDefinition();
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/orders/special"] = exactPathItem,
                ["/orders/{orderId}"] = templatePathItem,
            }
        };

        var resolvedPath = StubRouteResolver.ResolvePath(document, "/orders/special");

        Assert.NotNull(resolvedPath);
        Assert.Equal("/orders/special", resolvedPath.Value.PathPattern);
        Assert.Same(exactPathItem, resolvedPath.Value.PathItem);
    }

    [Fact]
    public void ResolvePath_PrefersMoreSpecificTemplatePath()
    {
        var specificPathItem = new PathItemDefinition();
        var broadPathItem = new PathItemDefinition();
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/orders/{orderId}/items/special"] = specificPathItem,
                ["/orders/{orderId}/items/{itemId}"] = broadPathItem,
            }
        };

        var resolvedPath = StubRouteResolver.ResolvePath(document, "/orders/123/items/special");

        Assert.NotNull(resolvedPath);
        Assert.Equal("/orders/{orderId}/items/special", resolvedPath.Value.PathPattern);
        Assert.Same(specificPathItem, resolvedPath.Value.PathItem);
    }

    [Fact]
    public void NormalizeMethod_DefaultsBlankMethodToGet()
    {
        var normalizedMethod = StubRouteResolver.NormalizeMethod("  ");

        Assert.Equal(HttpMethods.Get, normalizedMethod);
    }

    [Fact]
    public void NormalizePath_AddsLeadingSlashWhenMissing()
    {
        var normalizedPath = StubRouteResolver.NormalizePath("orders/123");

        Assert.Equal("/orders/123", normalizedPath);
    }
}
