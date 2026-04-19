using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services.Resolution;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Resolution;

public sealed class StubResponseHeaderBuilderTests
{
    [Fact]
    public void BuildResponseHeaders_JoinsNonCookieSequenceValues()
    {
        var headers = new Dictionary<string, HeaderDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Ids"] = new()
            {
                Example = new object?[] { 1, "two", true }
            }
        };

        var resolvedHeaders = StubResponseHeaderBuilder.BuildResponseHeaders(headers);

        Assert.Equal("1, two, true", resolvedHeaders["X-Ids"].ToString());
    }

    [Fact]
    public void BuildResponseHeaders_PreservesSeparateSetCookieValues()
    {
        var headers = new Dictionary<string, HeaderDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Set-Cookie"] = new()
            {
                Example = new object?[] { "session=abc; Path=/", "theme=dark; Path=/" }
            }
        };

        var resolvedHeaders = StubResponseHeaderBuilder.BuildResponseHeaders(headers);

        Assert.Equal(new StringValues(["session=abc; Path=/", "theme=dark; Path=/"]), resolvedHeaders["Set-Cookie"]);
    }
}
