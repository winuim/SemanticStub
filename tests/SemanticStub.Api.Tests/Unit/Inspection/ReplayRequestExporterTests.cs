using SemanticStub.Api.Inspection;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Inspection;

public sealed class ReplayRequestExporterTests
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
    public void Export_SimpleGetRequest_ReturnsMethodAndPath()
    {
        var request = MakeRequest("GET", "/hello");

        var result = ReplayRequestExporter.Export(request);

        Assert.Equal("GET", result.Method);
        Assert.Equal("/hello", result.Path);
    }

    [Fact]
    public void Export_MethodIsUpperCased()
    {
        var request = MakeRequest("get", "/hello");

        var result = ReplayRequestExporter.Export(request);

        Assert.Equal("GET", result.Method);
    }

    [Fact]
    public void Export_PostRequest_PreservesMethod()
    {
        var request = MakeRequest("POST", "/orders");

        var result = ReplayRequestExporter.Export(request);

        Assert.Equal("POST", result.Method);
    }

    [Fact]
    public void Export_WithQuery_PreservesQuery()
    {
        var query = new Dictionary<string, string[]>
        {
            ["role"] = ["admin"],
            ["active"] = ["true"],
        };
        var request = MakeRequest("GET", "/users", query: query);

        var result = ReplayRequestExporter.Export(request);

        Assert.NotNull(result.Query);
        Assert.Equal(["admin"], result.Query["role"]);
        Assert.Equal(["true"], result.Query["active"]);
    }

    [Fact]
    public void Export_WithMultiValueQuery_PreservesAllValues()
    {
        var query = new Dictionary<string, string[]>
        {
            ["id"] = ["1", "2", "3"],
        };
        var request = MakeRequest("GET", "/items", query: query);

        var result = ReplayRequestExporter.Export(request);

        Assert.NotNull(result.Query);
        Assert.Equal(["1", "2", "3"], result.Query["id"]);
    }

    [Fact]
    public void Export_WithNullQuery_ReturnsNullQuery()
    {
        var request = MakeRequest("GET", "/hello", query: null);

        var result = ReplayRequestExporter.Export(request);

        Assert.Null(result.Query);
    }

    [Fact]
    public void Export_WithHeaders_PreservesNonTransportHeaders()
    {
        var request = MakeRequest("GET", "/hello", headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = "application/json",
            ["Accept"] = "application/json",
        });

        var result = ReplayRequestExporter.Export(request);

        Assert.NotNull(result.Headers);
        Assert.Equal("application/json", result.Headers["Content-Type"]);
        Assert.Equal("application/json", result.Headers["Accept"]);
    }

    [Fact]
    public void Export_FiltersTransportHeaders()
    {
        var request = MakeRequest("GET", "/hello", headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Host"] = "localhost:5000",
            ["Connection"] = "keep-alive",
            ["Content-Length"] = "42",
            ["Transfer-Encoding"] = "chunked",
            ["Accept"] = "application/json",
        });

        var result = ReplayRequestExporter.Export(request);

        Assert.NotNull(result.Headers);
        Assert.False(result.Headers.ContainsKey("Host"));
        Assert.False(result.Headers.ContainsKey("Connection"));
        Assert.False(result.Headers.ContainsKey("Content-Length"));
        Assert.False(result.Headers.ContainsKey("Transfer-Encoding"));
        Assert.True(result.Headers.ContainsKey("Accept"));
    }

    [Fact]
    public void Export_WithOnlyTransportHeaders_ReturnsNullHeaders()
    {
        var request = MakeRequest("GET", "/hello", headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Host"] = "localhost:5000",
            ["Connection"] = "keep-alive",
        });

        var result = ReplayRequestExporter.Export(request);

        Assert.Null(result.Headers);
    }

    [Fact]
    public void Export_WithNullHeaders_ReturnsNullHeaders()
    {
        var request = MakeRequest("GET", "/hello", headers: null);

        var result = ReplayRequestExporter.Export(request);

        Assert.Null(result.Headers);
    }

    [Fact]
    public void Export_WithBody_PreservesBody()
    {
        var request = MakeRequest("POST", "/orders", body: "{\"name\":\"test\"}");

        var result = ReplayRequestExporter.Export(request);

        Assert.Equal("{\"name\":\"test\"}", result.Body);
    }

    [Fact]
    public void Export_WithNullBody_ReturnsNullBody()
    {
        var request = MakeRequest("POST", "/orders", body: null);

        var result = ReplayRequestExporter.Export(request);

        Assert.Null(result.Body);
    }

    [Fact]
    public void Export_WithEmptyBody_ReturnsEmptyBody()
    {
        var request = MakeRequest("POST", "/orders", body: string.Empty);

        var result = ReplayRequestExporter.Export(request);

        Assert.Equal(string.Empty, result.Body);
    }

    [Fact]
    public void Export_WithAllFields_MapsAllReplayFields()
    {
        var request = MakeRequest(
            method: "PUT",
            path: "/items/42",
            query: new Dictionary<string, string[]> { ["dry-run"] = ["true"] },
            headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "application/json",
                ["X-Request-Id"] = "abc123",
            },
            body: "{\"value\":1}");

        var result = ReplayRequestExporter.Export(request);

        Assert.Equal("PUT", result.Method);
        Assert.Equal("/items/42", result.Path);
        Assert.NotNull(result.Query);
        Assert.Equal(["true"], result.Query["dry-run"]);
        Assert.NotNull(result.Headers);
        Assert.Equal("application/json", result.Headers["Content-Type"]);
        Assert.Equal("abc123", result.Headers["X-Request-Id"]);
        Assert.Equal("{\"value\":1}", result.Body);
    }

    [Fact]
    public void Export_DoesNotIncludeRuntimeMetadata()
    {
        var request = new RecentRequestInfo
        {
            Method = "GET",
            Path = "/hello",
            StatusCode = 200,
            ElapsedMilliseconds = 42.5,
            RouteId = "some-route",
            MatchMode = "exact",
            FailureReason = null,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var result = ReplayRequestExporter.Export(request);

        // ReplayReadyRequestInfo has no runtime metadata fields
        Assert.Equal("GET", result.Method);
        Assert.Equal("/hello", result.Path);
        Assert.Null(result.Query);
        Assert.Null(result.Headers);
        Assert.Null(result.Body);
    }

    [Fact]
    public void Export_ThrowsWhenRequestIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ReplayRequestExporter.Export(null!));
    }
}
