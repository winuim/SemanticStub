using Microsoft.AspNetCore.Http;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Resolution;

public sealed class StubOperationResolverTests
{
    [Fact]
    public void TryResolveOperation_ReturnsMethodNotAllowedWhenPathMatchesButMethodDoesNot()
    {
        var pathItem = new PathItemDefinition
        {
            Get = new OperationDefinition(),
        };
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/orders/{orderId}"] = pathItem,
            }
        };

        var resolved = StubOperationResolver.TryResolveOperation(
            document,
            HttpMethods.Post,
            "/orders/123",
            out var pathPattern,
            out var resolvedPathItem,
            out _,
            out var failedMatchResult);

        Assert.False(resolved);
        Assert.Equal(StubMatchResult.MethodNotAllowed, failedMatchResult);
        Assert.Equal("/orders/{orderId}", pathPattern);
        Assert.Same(pathItem, resolvedPathItem);
    }

    [Fact]
    public void TryResolveOperation_ReturnsPathNotFoundWhenNoPathMatches()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/orders/{orderId}"] = new()
                {
                    Get = new OperationDefinition(),
                },
            }
        };

        var resolved = StubOperationResolver.TryResolveOperation(
            document,
            HttpMethods.Get,
            "/customers/123",
            out var pathPattern,
            out var pathItem,
            out var operation,
            out var failedMatchResult);

        Assert.False(resolved);
        Assert.Equal(StubMatchResult.PathNotFound, failedMatchResult);
        Assert.Equal(string.Empty, pathPattern);
        Assert.Null(pathItem);
        Assert.Null(operation);
    }
}
