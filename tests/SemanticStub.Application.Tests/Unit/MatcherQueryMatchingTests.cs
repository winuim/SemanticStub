using Microsoft.Extensions.Primitives;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services;
using System.Diagnostics;
using Xunit;

namespace SemanticStub.Application.Tests.Unit;

public sealed class MatcherQueryMatchingTests
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

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
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

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
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

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
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

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
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

        var matcher = CreateMatcherService();

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

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
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

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
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

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
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
    public void FindBestMatch_MatchesRegexQueryValueAsContainsReplacement()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                    {
                        Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["role"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["regex"] = ".*admin.*"
                            }
                        }
                    }
            ]
        };

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = "super-admin"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_PrefersEqualsQueryOverRegexQuery()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                    {
                        Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["role"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["regex"] = ".*admin.*"
                            }
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

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = "admin"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
        Assert.Equal("admin", match.Query["role"]);
    }

    [Fact]
    public void FindBestMatch_MatchesRepeatedRegexQueryValuesInOrder()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                    {
                        Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["tag"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["regex"] = new List<object?> { ".*alpha.*", ".*beta.*" }
                            }
                        }
                    }
            ]
        };

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
            operation,
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["tag"] = new StringValues(["pre-alpha", "beta-post"])
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_MatchesEqualsOperatorQueryValue()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                    {
                        Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["role"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["equals"] = "admin"
                            }
                        }
                    }
            ]
        };

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = "admin"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_ReturnsNullWhenEqualsOperatorQueryDoesNotMatch()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                    {
                        Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["role"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["equals"] = "admin"
                            }
                        }
                    }
            ]
        };

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
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
    public void FindBestMatch_MatchesRegexSingleQueryValue()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                    {
                        Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["role"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["regex"] = "^admin-[0-9]+$"
                            }
                        }
                    }
            ]
        };

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = "admin-42"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: null);

        Assert.NotNull(match);
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
                        Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["role"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["regex"] = "^admin-[0-9]+$"
                            }
                        }
                    }
            ]
        };

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
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
                        Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["role"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["regex"] = "^(a+)+$"
                            }
                        }
                    }
            ]
        };

        var matcher = CreateMatcherService();
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = new string('a', 4096) + "!"
        };

        var stopwatch = Stopwatch.StartNew();

        var match = FindBestMatch(
            matcher,
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
            Query = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["regex"] = ".*admin.*"
                }
            },
            Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-tenant"] = "alpha"
            }
        };

        var broaderRegexMatch = new QueryMatchDefinition
        {
            Query = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["regex"] = "^admin-[0-9]+$"
                }
            }
        };

        var operation = new OperationDefinition
        {
            Matches = [broaderRegexMatch, specificHeaderMatch]
        };

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
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
        Assert.Equal("alpha", match.Headers["x-tenant"]);
    }
}
