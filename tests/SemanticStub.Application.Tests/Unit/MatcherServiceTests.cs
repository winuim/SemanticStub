using Microsoft.Extensions.Primitives;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services;
using System.Diagnostics;
using Xunit;

namespace SemanticStub.Application.Tests.Unit;

public sealed class MatcherServiceTests
{
    private static MatcherService CreateMatcherService()
    {
        return new MatcherService(new JsonBodyMatcher(), new FormBodyMatcher(), new QueryValueMatcher(), new RegexQueryMatcher());
    }

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

        var matcher = CreateMatcherService();
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["role"] = "admin",
            ["view"] = "summary"
        };

        var match = FindBestMatch(
            matcher,
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
    public void FindBestMatch_UsesPathParametersOperationQueryHeadersAndBody()
    {
        var pathParameters = new[]
        {
            new ParameterDefinition
            {
                Name = "tenantId",
                In = "path",
                Schema = new ParameterSchemaDefinition
                {
                    Type = "string"
                }
            }
        };

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
                        ["page"] = 2
                    },
                    Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["X-Env"] = "staging"
                    },
                    Body = new Dictionary<object, object>
                    {
                        ["username"] = "demo"
                    }
                }
            ]
        };

        var matcher = CreateMatcherService();

        var match = matcher.FindBestMatch(
            pathParameters,
            operation,
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["page"] = "2"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-env"] = "staging"
            },
            "{\"username\":\"demo\",\"rememberMe\":true}");

        Assert.NotNull(match);
        Assert.Equal(2, match.Query["page"]);
        Assert.Equal("staging", match.Headers["X-Env"]);
        var body = Assert.IsAssignableFrom<IDictionary<object, object>>(match.Body);
        Assert.Equal("demo", body["username"]);
    }

    [Fact]
    public void EvaluateCandidates_ReturnsPerDimensionResults()
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
                    Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["X-Env"] = "staging"
                    }
                },
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["role"] = "guest"
                    }
                }
            ]
        };

        var matcher = CreateMatcherService();

        var evaluations = matcher.EvaluateCandidates(
            [],
            operation,
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["role"] = "admin"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Env"] = "staging"
            },
            body: null);

        Assert.Equal(2, evaluations.Count);
        Assert.True(evaluations[0].Matched);
        Assert.True(evaluations[0].HeaderMatched);
        Assert.False(evaluations[1].Matched);
        Assert.False(evaluations[1].QueryMatched);
    }

    [Fact]
    public void EvaluateCandidates_UsesSharedPreprocessingForTypedQueryAndInvalidBody()
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
                        ["page"] = 2
                    },
                    Body = new Dictionary<object, object>
                    {
                        ["username"] = "demo"
                    }
                }
            ]
        };

        var matcher = CreateMatcherService();

        var evaluations = matcher.EvaluateCandidates(
            [],
            operation,
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["page"] = "2"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "{not-json");

        var evaluation = Assert.Single(evaluations);
        Assert.True(evaluation.QueryMatched);
        Assert.False(evaluation.BodyMatched);
        Assert.False(evaluation.Matched);
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
}
