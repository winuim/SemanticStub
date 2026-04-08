using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class QueryMatchSpecificityComparerTests
{
    [Fact]
    public void Compare_PrefersExactQuerySpecificityBeforeOverallSpecificity()
    {
        var broaderOverallMatch = new QueryMatchDefinition
        {
            PartialQuery = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = "admin"
            },
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-tenant"] = "alpha"
            }
        };

        var exactQueryMatch = new QueryMatchDefinition
        {
            Query = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = "admin"
            }
        };

        var ordered = new[] { broaderOverallMatch, exactQueryMatch }
            .OrderBy(match => match, QueryMatchSpecificityComparer.Instance)
            .ToArray();

        Assert.Same(exactQueryMatch, ordered[0]);
    }

    [Fact]
    public void Compare_PrefersRegexQuerySpecificityOnlyAfterOverallSpecificityTie()
    {
        var specificHeaderMatch = new QueryMatchDefinition
        {
            PartialQuery = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = "admin"
            },
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-tenant"] = "alpha"
            }
        };

        var broaderRegexMatch = new QueryMatchDefinition
        {
            RegexQuery = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = "^admin-[0-9]+$"
            }
        };

        var ordered = new[] { broaderRegexMatch, specificHeaderMatch }
            .OrderBy(match => match, QueryMatchSpecificityComparer.Instance)
            .ToArray();

        Assert.Same(specificHeaderMatch, ordered[0]);
    }
}
