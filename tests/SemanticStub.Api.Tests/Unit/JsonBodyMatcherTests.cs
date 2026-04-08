using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class JsonBodyMatcherTests
{
    [Fact]
    public void ParseRequestBody_ReturnsNullForInvalidJson()
    {
        var matcher = new JsonBodyMatcher();

        using var bodyDocument = matcher.ParseRequestBody("{not-json");

        Assert.Null(bodyDocument);
    }

    [Fact]
    public void IsMatch_AllowsPartialObjectMatches()
    {
        var matcher = new JsonBodyMatcher();
        using var actualBody = matcher.ParseRequestBody("{\"username\":\"demo\",\"rememberMe\":true}");

        var matched = matcher.IsMatch(
            new Dictionary<object, object>
            {
                ["username"] = "demo"
            },
            actualBody?.RootElement);

        Assert.True(matched);
    }

    [Fact]
    public void IsMatch_ReturnsFalseForUnsupportedBodyDefinitions()
    {
        var matcher = new JsonBodyMatcher();
        using var actualBody = matcher.ParseRequestBody("{\"value\":42}");

        var matched = matcher.IsMatch(
            new Dictionary<object, object>
            {
                ["value"] = new IntPtr(42)
            },
            actualBody?.RootElement);

        Assert.False(matched);
    }

    [Fact]
    public void IsMatch_RequiresArrayLengthAndOrderToMatch()
    {
        var matcher = new JsonBodyMatcher();
        using var actualBody = matcher.ParseRequestBody("{\"items\":[1,2]}");

        var matched = matcher.IsMatch(
            new Dictionary<object, object>
            {
                ["items"] = new List<object> { 2, 1 }
            },
            actualBody?.RootElement);

        Assert.False(matched);
    }
}
