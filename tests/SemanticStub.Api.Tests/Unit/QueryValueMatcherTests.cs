using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

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
    public void IsPartialMatch_UsesTypedEqualityForBooleanValues()
    {
        var matcher = new QueryValueMatcher();

        var matched = matcher.IsPartialMatch(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["enabled"] = true
            },
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["enabled"] = new StringValues(["prefix-true", "true", "suffix"])
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["enabled"] = "boolean"
            });

        Assert.True(matched);
    }

    [Fact]
    public void IsPartialMatch_MatchesPlainStringAsSubstring()
    {
        var matcher = new QueryValueMatcher();

        var matched = matcher.IsPartialMatch(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = "admin"
            },
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["role"] = new StringValues("super-admin")
            },
            queryParameterTypes: new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.True(matched);
    }

    [Fact]
    public void IsPartialMatch_ReturnsTrueWhenExpectedRepeatedValueSequenceIsEmpty()
    {
        var matcher = new QueryValueMatcher();

        var matched = matcher.IsPartialMatch(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tag"] = new List<object?>()
            },
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["tag"] = new StringValues(["alpha", "beta"])
            },
            queryParameterTypes: new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.True(matched);
    }

    [Fact]
    public void IsPartialMatch_ReturnsFalseWhenRepeatedValuesAppearOutOfOrder()
    {
        var matcher = new QueryValueMatcher();

        var matched = matcher.IsPartialMatch(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tag"] = new List<object?> { "alpha", "beta" }
            },
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["tag"] = new StringValues(["beta-post", "alpha-pre"])
            },
            queryParameterTypes: new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.False(matched);
    }
}
