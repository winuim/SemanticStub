using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using System.Diagnostics;
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
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
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
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
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
    public void FindBestMatch_OldOverloadRemainsCompatible()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["role"] = "admin"
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = "admin"
            },
            body: null);

        Assert.NotNull(match);
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
    public void FindBestMatch_MatchesIntegerQueryUsingDeclaredSchemaType()
    {
        var operation = new OperationDefinition
        {
            Parameters =
            [
                new ParameterDefinition
                {
                    Name = "page",
                    In = "query",
                    Schema = new ParameterSchemaDefinition
                    {
                        Type = "integer"
                    }
                }
            ],
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["page"] = 1
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page"] = "1"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_MatchesNumberQueryUsingDeclaredSchemaType()
    {
        var operation = new OperationDefinition
        {
            Parameters =
            [
                new ParameterDefinition
                {
                    Name = "ratio",
                    In = "query",
                    Schema = new ParameterSchemaDefinition
                    {
                        Type = "number"
                    }
                }
            ],
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["ratio"] = 1.5m
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ratio"] = "1.5"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_MatchesBooleanQueryUsingDeclaredSchemaType()
    {
        var operation = new OperationDefinition
        {
            Parameters =
            [
                new ParameterDefinition
                {
                    Name = "enabled",
                    In = "query",
                    Schema = new ParameterSchemaDefinition
                    {
                        Type = "boolean"
                    }
                }
            ],
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["enabled"] = true
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["enabled"] = "true"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_ReturnsNullWhenTypedQueryValueDoesNotMatch()
    {
        var operation = new OperationDefinition
        {
            Parameters =
            [
                new ParameterDefinition
                {
                    Name = "page",
                    In = "query",
                    Schema = new ParameterSchemaDefinition
                    {
                        Type = "integer"
                    }
                }
            ],
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["page"] = 1
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page"] = "1.5"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.Null(match);
    }

    [Fact]
    public void FindBestMatch_UsesPathLevelQueryParameterType()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["page"] = 1
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            [
                new ParameterDefinition
                {
                    Name = "page",
                    In = "query",
                    Schema = new ParameterSchemaDefinition
                    {
                        Type = "integer"
                    }
                }
            ],
            operation,
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(StringComparer.Ordinal)
            {
                ["page"] = new("1")
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_MatchesMultiValueQueryInOrder()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["tag"] = new List<object?> { "alpha", "beta" }
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["tag"] = new StringValues(["alpha", "beta"])
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_ReturnsNullWhenMultiValueQueryOrderDiffers()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["tag"] = new List<object?> { "alpha", "beta" }
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["tag"] = new StringValues(["beta", "alpha"])
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.Null(match);
    }

    [Fact]
    public void FindBestMatch_ReturnsNullWhenSingleValueMatchReceivesMultipleActualValues()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["tag"] = "alpha"
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["tag"] = new StringValues(["alpha", "beta"])
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.Null(match);
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

    [Fact]
    public void FindBestMatch_MatchesPartialSingleQueryValue()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    PartialQuery = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["role"] = "admin"
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = "super-admin"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
        Assert.Equal("admin", match.PartialQuery["role"]);
    }

    [Fact]
    public void FindBestMatch_PrefersExactQueryOverPartialQuery()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    PartialQuery = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["role"] = "admin"
                    }
                },
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["role"] = "admin"
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = "admin"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
        Assert.Equal("admin", match.Query["role"]);
        Assert.Empty(match.PartialQuery);
    }

    [Fact]
    public void FindBestMatch_MatchesPartialRepeatedQueryValuesInOrder()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    PartialQuery = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["tag"] = new List<object?> { "alpha", "beta" }
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["tag"] = new StringValues(["pre-alpha", "middle", "beta-post"])
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_DoesNotTreatNullPartialQueryValueAsWildcard()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    PartialQuery = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["role"] = null
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = "admin"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.Null(match);
    }

    [Fact]
    public void FindBestMatch_MatchesNullPartialQueryValueOnlyAgainstEmptyString()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    PartialQuery = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["role"] = null
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = string.Empty
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_MatchesRegexSingleQueryValue()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    RegexQuery = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["role"] = "^admin-[0-9]+$"
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = "admin-42"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
        Assert.Equal("^admin-[0-9]+$", match.RegexQuery["role"]);
    }

    [Fact]
    public void FindBestMatch_ReturnsNullWhenRegexQueryDoesNotMatch()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    RegexQuery = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["role"] = "^admin-[0-9]+$"
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = "guest"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.Null(match);
    }

    [Fact]
    public void FindBestMatch_ReturnsNullWhenRegexQueryEvaluationTimesOut()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    RegexQuery = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["role"] = "^(a+)+$"
                    }
                }
            ]
        };

        var matcher = new MatcherService();
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = new string('a', 4096) + "!"
        };

        var stopwatch = Stopwatch.StartNew();

        var match = matcher.FindBestMatch(
            operation,
            query,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        stopwatch.Stop();

        Assert.Null(match);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void FindBestMatch_PreservesOverallSpecificityBeforeRegexTieBreaks()
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

        var operation = new OperationDefinition
        {
            Matches = [broaderRegexMatch, specificHeaderMatch]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = "admin-42"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-tenant"] = "alpha"
            },
            body: null);

        Assert.NotNull(match);
        Assert.Equal("admin", match.PartialQuery["role"]);
        Assert.Equal("alpha", match.Headers["x-tenant"]);
        Assert.Empty(match.RegexQuery);
    }

    [Fact]
    public void FindBestMatch_PrefersRegexQueryOverPartialQuery()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    PartialQuery = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["role"] = "admin"
                    }
                },
                new QueryMatchDefinition
                {
                    RegexQuery = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["role"] = "^admin-[0-9]+$"
                    }
                }
            ]
        };

        var matcher = new MatcherService();

        var match = matcher.FindBestMatch(
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = "admin-42"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
        Assert.Equal("^admin-[0-9]+$", match.RegexQuery["role"]);
        Assert.Empty(match.PartialQuery);
    }
}
