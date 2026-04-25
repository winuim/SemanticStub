using SemanticStub.Api.Inspection;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Inspection;

public sealed class DraftYamlExporterTests
{
    private static ReplayReadyRequestInfo MakeRequest(
        string method = "GET",
        string path = "/hello",
        IReadOnlyDictionary<string, string[]>? query = null,
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null)
    {
        return new ReplayReadyRequestInfo
        {
            Method = method,
            Path = path,
            Query = query,
            Headers = headers,
            Body = body,
        };
    }

    [Fact]
    public void Export_SimpleGetRequest_ContainsOpenApiHeader()
    {
        var request = MakeRequest("GET", "/hello");

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("openapi: 3.1.0", result);
        Assert.Contains("title: Draft Stub", result);
    }

    [Fact]
    public void Export_SimpleGetRequest_ContainsPathAndMethod()
    {
        var request = MakeRequest("GET", "/hello");

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("  /hello:", result);
        Assert.Contains("    get:", result);
    }

    [Fact]
    public void Export_SimpleGetRequest_ContainsOperationId()
    {
        var request = MakeRequest("GET", "/hello");

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("operationId: getHello", result);
    }

    [Fact]
    public void Export_PostMethod_LowercaseInYaml()
    {
        var request = MakeRequest("POST", "/login");

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("    post:", result);
        Assert.Contains("operationId: postLogin", result);
    }

    [Fact]
    public void Export_PutMethod_GeneratesCorrectOperationId()
    {
        var request = MakeRequest("PUT", "/profile");

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("operationId: putProfile", result);
    }

    [Fact]
    public void Export_PathWithMultipleSegments_GeneratesCorrectOperationId()
    {
        var request = MakeRequest("GET", "/orders/special");

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("operationId: getOrdersSpecial", result);
    }

    [Fact]
    public void Export_WithNoQueryOrHeaders_OmitsXMatch()
    {
        var request = MakeRequest("GET", "/hello");

        var result = DraftYamlExporter.Export(request);

        Assert.DoesNotContain("x-match:", result);
    }

    [Fact]
    public void Export_WithQueryParams_IncludesXMatch()
    {
        var request = MakeRequest("GET", "/users", query: new Dictionary<string, string[]>
        {
            ["role"] = ["admin"],
        });

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("x-match:", result);
        Assert.Contains("query:", result);
        Assert.Contains("role: admin", result);
    }

    [Fact]
    public void Export_WithMultipleQueryParams_IncludesAll()
    {
        var request = MakeRequest("GET", "/users", query: new Dictionary<string, string[]>
        {
            ["role"] = ["admin"],
            ["active"] = ["1"],
        });

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("role: admin", result);
        Assert.Contains("active: 1", result);
    }

    [Fact]
    public void Export_WithMultiValueQuery_GeneratesArraySyntax()
    {
        var request = MakeRequest("GET", "/search", query: new Dictionary<string, string[]>
        {
            ["tag"] = ["alpha", "beta"],
        });

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("tag:", result);
        Assert.Contains("- alpha", result);
        Assert.Contains("- beta", result);
    }

    [Fact]
    public void Export_WithCustomHeader_IncludesInXMatch()
    {
        var request = MakeRequest("GET", "/env-users", headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Env"] = "staging",
        });

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("x-match:", result);
        Assert.Contains("headers:", result);
        Assert.Contains("X-Env: staging", result);
    }

    [Fact]
    public void Export_WithSensitiveHeaders_ExcludesThemFromXMatch()
    {
        var request = MakeRequest("GET", "/secure", headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = "Bearer secret-token",
            ["Cookie"] = "session=abc",
            ["Proxy-Authorization"] = "Basic xyz",
        });

        var result = DraftYamlExporter.Export(request);

        Assert.DoesNotContain("Authorization", result);
        Assert.DoesNotContain("Cookie", result);
        Assert.DoesNotContain("Proxy-Authorization", result);
        Assert.DoesNotContain("x-match:", result);
    }

    [Fact]
    public void Export_WithLowConfidenceHeaders_ExcludesThemFromXMatch()
    {
        var request = MakeRequest("GET", "/hello", headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Accept"] = "application/json",
            ["Accept-Encoding"] = "gzip",
            ["Accept-Language"] = "en-US",
            ["User-Agent"] = "Mozilla/5.0",
            ["Referer"] = "https://example.com",
            ["Origin"] = "https://example.com",
        });

        var result = DraftYamlExporter.Export(request);

        Assert.DoesNotContain("x-match:", result);
    }

    [Fact]
    public void Export_WithContentTypeHeaderOnly_ExcludesFromXMatch()
    {
        var request = MakeRequest("POST", "/login", headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = "application/json",
        });

        var result = DraftYamlExporter.Export(request);

        Assert.DoesNotContain("x-match:", result);
    }

    [Fact]
    public void Export_WithJsonBody_IncludesBodyInXMatch()
    {
        var request = MakeRequest("POST", "/login",
            headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "application/json",
            },
            body: """{"username":"demo","password":"secret"}""");

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("x-match:", result);
        Assert.Contains("body:", result);
        Assert.Contains("username: demo", result);
        Assert.Contains("password: secret", result);
    }

    [Fact]
    public void Export_WithNonJsonBody_OmitsBodyFromXMatch()
    {
        var request = MakeRequest("POST", "/form",
            headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "application/x-www-form-urlencoded",
            },
            body: "grant_type=authorization_code&code=abc");

        var result = DraftYamlExporter.Export(request);

        Assert.DoesNotContain("x-match:", result);
    }

    [Fact]
    public void Export_WithInvalidJsonBody_OmitsBodyFromXMatch()
    {
        var request = MakeRequest("POST", "/data",
            headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "application/json",
            },
            body: "not-valid-json");

        var result = DraftYamlExporter.Export(request);

        Assert.DoesNotContain("x-match:", result);
    }

    [Fact]
    public void Export_WithJsonArrayBody_OmitsBodyFromXMatch()
    {
        var request = MakeRequest("POST", "/data",
            headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "application/json",
            },
            body: "[1,2,3]");

        var result = DraftYamlExporter.Export(request);

        Assert.DoesNotContain("x-match:", result);
    }

    [Fact]
    public void Export_WithBody_IncludesRequestBody()
    {
        var request = MakeRequest("POST", "/login",
            headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "application/json",
            },
            body: """{"username":"demo"}""");

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("requestBody:", result);
        Assert.Contains("application/json:", result);
    }

    [Fact]
    public void Export_WithNoBody_OmitsRequestBody()
    {
        var request = MakeRequest("GET", "/hello");

        var result = DraftYamlExporter.Export(request);

        Assert.DoesNotContain("requestBody:", result);
    }

    [Fact]
    public void Export_AlwaysContainsResponsesSection()
    {
        var request = MakeRequest("GET", "/hello");

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("responses:", result);
        Assert.Contains("'200':", result);
        Assert.Contains("description: TODO", result);
    }

    [Fact]
    public void Export_XMatchResponseIsStatusCode200()
    {
        var request = MakeRequest("GET", "/users", query: new Dictionary<string, string[]>
        {
            ["role"] = ["admin"],
        });

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("statusCode: 200", result);
    }

    [Fact]
    public void Export_XMatch_ConditionsBeforeResponse()
    {
        var request = MakeRequest("GET", "/users", query: new Dictionary<string, string[]>
        {
            ["role"] = ["admin"],
        });

        var result = DraftYamlExporter.Export(request);

        var queryIndex = result.IndexOf("query:", StringComparison.Ordinal);
        var responseIndex = result.IndexOf("response:", StringComparison.Ordinal);
        Assert.True(queryIndex < responseIndex, "query condition must appear before response in x-match");
    }

    [Fact]
    public void Export_PathWithPathParameter_GeneratesSafeOperationId()
    {
        var request = MakeRequest("GET", "/orders/{orderId}");

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("operationId: getOrdersOrderId", result);
    }

    [Fact]
    public void Export_WithNestedJsonBody_EmitsTodoComment()
    {
        var request = MakeRequest("POST", "/data",
            headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "application/json",
            },
            body: """{"name":"demo","address":{"city":"Tokyo"}}""");

        var result = DraftYamlExporter.Export(request);

        Assert.Contains("name: demo", result);
        Assert.Contains("TODO", result);
    }

    [Fact]
    public void Export_YamlScalar_EscapesApostropheWithDoubleApostrophe()
    {
        var request = MakeRequest("GET", "/users", query: new Dictionary<string, string[]>
        {
            ["q"] = ["it's"],
        });

        var result = DraftYamlExporter.Export(request);

        // YAML single-quoted scalar: apostrophe is escaped as ''
        Assert.Contains("q: 'it''s'", result);
    }

    [Fact]
    public void Export_ThrowsWhenRequestIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => DraftYamlExporter.Export(null!));
    }
}
