using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Application.Tests.Unit;

public sealed class QueryValueMatcherTests
{
    [Fact]
    public void IsExactMatch_MatchesTypedRepeatedValuesInOrder()
    {
        var matcher = new QueryValueMatcher();

        var matched = matcher.IsExactMatch(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["page"] = new List<object?> { 1, 2 }
            },
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["page"] = new StringValues(["1", "2"])
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page"] = "integer"
            });

        Assert.True(matched);
    }

    [Fact]
    public void IsExactMatch_ReturnsFalseWhenTypedRepeatedValueCountDiffers()
    {
        var matcher = new QueryValueMatcher();

        var matched = matcher.IsExactMatch(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["page"] = new List<object?> { 1, 2 }
            },
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["page"] = new StringValues(["1"])
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page"] = "integer"
            });

        Assert.False(matched);
    }

    [Fact]
    public void IsExactMatch_MatchesNumberValuesUsingDeclaredType()
    {
        var matcher = new QueryValueMatcher();

        var matched = matcher.IsExactMatch(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ratio"] = 1.5m
            },
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["ratio"] = new StringValues("1.5")
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ratio"] = "number"
            });

        Assert.True(matched);
    }

    [Fact]
    public void IsExactMatch_MatchesEqualsOperatorUsingDeclaredType()
    {
        var matcher = new QueryValueMatcher();

        var matched = matcher.IsExactMatch(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["enabled"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["equals"] = true
                }
            },
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["enabled"] = new StringValues("true")
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["enabled"] = "boolean"
            });

        Assert.True(matched);
    }

}
