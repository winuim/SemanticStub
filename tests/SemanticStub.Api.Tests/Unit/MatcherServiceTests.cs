using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class MatcherServiceTests
{
    [Fact]
    public void FindBestMatch_PrefersMoreSpecificQueryAndBodyMatch()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["role"] = "admin"
                    },
                    Body = new Dictionary<object, object>
                    {
                        ["username"] = "demo"
                    }
                },
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["role"] = "admin",
                        ["view"] = "summary"
                    },
                    Body = new Dictionary<object, object>
                    {
                        ["username"] = "demo",
                        ["password"] = "secret"
                    }
                }
            ]
        };

        var matcher = new MatcherService();
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin",
            ["view"] = "summary"
        };

        var match = matcher.FindBestMatch(
            operation,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "{\"username\":\"demo\",\"password\":\"secret\"}");

        Assert.NotNull(match);
        Assert.Equal(2, match.Query.Count);
        var body = Assert.IsAssignableFrom<IDictionary<object, object>>(match.Body);
        Assert.Equal("secret", body["password"]);
    }

    [Fact]
    public void FindBestMatch_AllowsPartialObjectBodyMatch()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Body = new Dictionary<object, object>
                    {
                        ["username"] = "demo"
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "{\"username\":\"demo\",\"rememberMe\":true}");

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_ReturnsNullWhenBodyIsInvalidJson()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Body = new Dictionary<object, object>
                    {
                        ["username"] = "demo"
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "{not-json");

        Assert.Null(match);
    }

    [Fact]
    public void FindBestMatch_MatchesHeadersUsingCaseInsensitiveHeaderNames()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["X-Env"] = "staging"
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-env"] = "staging"
            },
            body: null);

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_PrefersCandidateWithMoreSpecificHeaders()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["X-Env"] = "staging"
                    }
                },
                new QueryMatchDefinition
                {
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["X-Env"] = "staging",
                        ["X-Tenant"] = "alpha"
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-env"] = "staging",
                ["x-tenant"] = "alpha"
            },
            body: null);

        Assert.NotNull(match);
        Assert.Equal(2, match.Headers.Count);
    }
}
