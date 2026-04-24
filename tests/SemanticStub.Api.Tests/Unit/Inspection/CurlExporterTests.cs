using SemanticStub.Api.Inspection;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Inspection;

public sealed class CurlExporterTests
{
    private static RecentRequestInfo MakeRequest(
        string method = "GET",
        string path = "/hello",
        IReadOnlyDictionary<string, string[]>? query = null,
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null)
    {
        return new RecentRequestInfo
        {
            Method = method,
            Path = path,
            Query = query,
            Headers = headers,
            Body = body,
        };
    }

    [Fact]
    public void Export_SimpleGetRequest_ReturnsCurlWithMethodAndUrl()
    {
        var request = MakeRequest("GET", "/hello");

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.Equal("curl -X GET 'http://localhost:5000/hello'", result);
    }

    [Fact]
    public void Export_PostRequest_IncludesMethod()
    {
        var request = MakeRequest("POST", "/orders");

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.StartsWith("curl -X POST", result);
    }

    [Fact]
    public void Export_WithQueryParameters_AppendsEncodedQueryString()
    {
        var request = MakeRequest("GET", "/users", query: new Dictionary<string, string[]>
        {
            ["role"] = ["admin"],
            ["active"] = ["true"],
        });

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.Contains("/users?", result);
        Assert.Contains("role=admin", result);
        Assert.Contains("active=true", result);
    }

    [Fact]
    public void Export_WithMultiValueQueryParameter_RepeatsKey()
    {
        var request = MakeRequest("GET", "/items", query: new Dictionary<string, string[]>
        {
            ["id"] = ["1", "2", "3"],
        });

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.Contains("id=1", result);
        Assert.Contains("id=2", result);
        Assert.Contains("id=3", result);
    }

    [Fact]
    public void Export_WithHeaders_AddsHeaderLines()
    {
        var request = MakeRequest("GET", "/hello", headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = "application/json",
            ["Accept"] = "application/json",
        });

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.Contains("-H 'Accept: application/json'", result);
        Assert.Contains("-H 'Content-Type: application/json'", result);
    }

    [Fact]
    public void Export_SkipsTransportHeaders()
    {
        var request = MakeRequest("GET", "/hello", headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Host"] = "localhost:5000",
            ["Connection"] = "keep-alive",
            ["Content-Length"] = "0",
            ["Transfer-Encoding"] = "chunked",
            ["Accept"] = "application/json",
        });

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.DoesNotContain("Host:", result);
        Assert.DoesNotContain("Connection:", result);
        Assert.DoesNotContain("Content-Length:", result);
        Assert.DoesNotContain("Transfer-Encoding:", result);
        Assert.Contains("-H 'Accept: application/json'", result);
    }

    [Fact]
    public void Export_WithBody_AddsDataRawFlag()
    {
        var request = MakeRequest("POST", "/orders", body: "{\"name\":\"test\"}");

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.Contains("--data-raw '{\"name\":\"test\"}'", result);
    }

    [Fact]
    public void Export_WithHeadersAndBody_OutputsMultilineCommand()
    {
        var request = MakeRequest("POST", "/orders",
            headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "application/json",
            },
            body: "{\"name\":\"test\"}");

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.Contains(" \\\n", result);
        Assert.Contains("-H 'Content-Type: application/json'", result);
        Assert.Contains("--data-raw", result);
    }

    [Fact]
    public void Export_EscapesSingleQuotesInUrl()
    {
        var request = MakeRequest("GET", "/it's/a/path");

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.Contains("it'\\''s", result);
    }

    [Fact]
    public void Export_EscapesSingleQuotesInBody()
    {
        var request = MakeRequest("POST", "/orders", body: "it's a value");

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.Contains("it'\\''s a value", result);
    }

    [Fact]
    public void Export_EscapesSingleQuotesInHeaders()
    {
        var request = MakeRequest("GET", "/hello", headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Custom"] = "val'ue",
        });

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.Contains("val'\\''ue", result);
    }

    [Fact]
    public void Export_WithNullQuery_DoesNotAppendQueryString()
    {
        var request = MakeRequest("GET", "/hello", query: null);

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.DoesNotContain("?", result);
    }

    [Fact]
    public void Export_WithNullHeaders_DoesNotAddHeaderLines()
    {
        var request = MakeRequest("GET", "/hello", headers: null);

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.DoesNotContain("-H", result);
    }

    [Fact]
    public void Export_WithNullBody_DoesNotAddDataRaw()
    {
        var request = MakeRequest("POST", "/orders", body: null);

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.DoesNotContain("--data-raw", result);
    }

    [Fact]
    public void Export_WithEmptyBody_DoesNotAddDataRaw()
    {
        var request = MakeRequest("POST", "/orders", body: string.Empty);

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.DoesNotContain("--data-raw", result);
    }

    [Fact]
    public void Export_HeadersAreSortedAlphabetically()
    {
        var request = MakeRequest("GET", "/hello", headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Request-Id"] = "abc",
            ["Accept"] = "application/json",
            ["Content-Type"] = "text/plain",
        });

        var result = CurlExporter.Export(request, "http://localhost:5000");

        var acceptPos = result.IndexOf("Accept:", StringComparison.Ordinal);
        var contentTypePos = result.IndexOf("Content-Type:", StringComparison.Ordinal);
        var xRequestIdPos = result.IndexOf("X-Request-Id:", StringComparison.Ordinal);

        Assert.True(acceptPos < contentTypePos);
        Assert.True(contentTypePos < xRequestIdPos);
    }

    [Fact]
    public void Export_QueryKeysAreSortedAlphabetically()
    {
        var request = MakeRequest("GET", "/search", query: new Dictionary<string, string[]>
        {
            ["z"] = ["last"],
            ["a"] = ["first"],
            ["m"] = ["middle"],
        });

        var result = CurlExporter.Export(request, "http://localhost:5000");

        var aPos = result.IndexOf("a=first", StringComparison.Ordinal);
        var mPos = result.IndexOf("m=middle", StringComparison.Ordinal);
        var zPos = result.IndexOf("z=last", StringComparison.Ordinal);

        Assert.True(aPos < mPos);
        Assert.True(mPos < zPos);
    }

    [Fact]
    public void Export_MethodIsUpperCased()
    {
        var request = MakeRequest("get", "/hello");

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.StartsWith("curl -X GET", result);
    }

    [Fact]
    public void Export_ThrowsWhenRequestIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => CurlExporter.Export(null!, "http://localhost:5000"));
    }

    [Fact]
    public void Export_ThrowsArgumentNullException_WhenBaseUrlIsNull()
    {
        var request = MakeRequest();

        Assert.Throws<ArgumentNullException>(() => CurlExporter.Export(request, null!));
    }

    [Fact]
    public void Export_ThrowsArgumentException_WhenBaseUrlIsEmpty()
    {
        var request = MakeRequest();

        Assert.Throws<ArgumentException>(() => CurlExporter.Export(request, string.Empty));
    }

    [Fact]
    public void Export_WithSpecialCharsInQueryValue_EncodesValue()
    {
        var request = MakeRequest("GET", "/search", query: new Dictionary<string, string[]>
        {
            ["q"] = ["hello world"],
        });

        var result = CurlExporter.Export(request, "http://localhost:5000");

        Assert.Contains("q=hello+world", result);
    }
}
