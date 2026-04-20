using Microsoft.Extensions.Primitives;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services;
using System.Diagnostics;
using Xunit;

namespace SemanticStub.Application.Tests.Unit;

public sealed class MatcherHeaderMatchingTests
{
    private static MatcherService CreateMatcherService()
    {
        return new MatcherService(new JsonBodyMatcher(), new FormBodyMatcher(), new QueryValueMatcher(), new RegexQueryMatcher());
    }

    private static QueryMatchDefinition? FindBestMatch(
        MatcherService matcher,
        OperationDefinition operation,
        IReadOnlyDictionary<string, string> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        return matcher.FindBestMatch(
            operation.Parameters,
            operation,
            query.ToDictionary(
                entry => entry.Key,
                entry => new StringValues(entry.Value),
                StringComparer.Ordinal),
            headers,
            body);
    }

    private static QueryMatchDefinition? FindBestMatch(
        MatcherService matcher,
        OperationDefinition operation,
        IReadOnlyDictionary<string, StringValues> query,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        return matcher.FindBestMatch(
            operation.Parameters,
            operation,
            query,
            headers,
            body);
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
                        Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["X-Env"] = "staging"
                        }
                    }
            ]
        };

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
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
                        Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["X-Env"] = "staging"
                        }
                    },
                    new QueryMatchDefinition
                    {
                        Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["X-Env"] = "staging",
                            ["X-Tenant"] = "alpha"
                        }
                    }
            ]
        };

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
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

    [Fact]
    public void FindBestMatch_MatchesRegexHeaders()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                    {
                        Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["X-Env"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["regex"] = "^stag.*$"
                            }
                        }
                    }
            ]
        };

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-env"] = "staging"
            },
            body: null);

        Assert.NotNull(match);
    }
}
