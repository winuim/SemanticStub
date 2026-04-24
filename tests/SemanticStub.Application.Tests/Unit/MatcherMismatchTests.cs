using Microsoft.Extensions.Primitives;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services;
using Xunit;

namespace SemanticStub.Application.Tests.Unit;

public sealed class MatcherMismatchTests
{
    private static MatcherService CreateMatcherService()
    {
        return new MatcherService(new JsonBodyMatcher(), new FormBodyMatcher(), new QueryValueMatcher(), new RegexQueryMatcher());
    }

    private static IReadOnlyDictionary<string, StringValues> Query(params (string key, string value)[] pairs)
    {
        return pairs.ToDictionary(
            p => p.key,
            p => new StringValues(p.value),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> Headers(params (string key, string value)[] pairs)
    {
        return pairs.ToDictionary(
            p => p.key,
            p => p.value,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateCandidates_QueryKeyMissing_EmitsMissingMismatch()
    {
        var matcher = CreateMatcherService();
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal) { ["role"] = "admin" },
                }
            ]
        };

        var evaluations = matcher.EvaluateCandidates([], operation, Query(), Headers(), body: null);

        var mismatch = Assert.Single(evaluations[0].MismatchReasons);
        Assert.Equal("query", mismatch.Dimension);
        Assert.Equal("role", mismatch.Key);
        Assert.Equal("admin", mismatch.Expected);
        Assert.Null(mismatch.Actual);
        Assert.Equal("missing", mismatch.Kind);
    }

    [Fact]
    public void EvaluateCandidates_QueryKeyPresentButWrongValue_EmitsUnequalMismatch()
    {
        var matcher = CreateMatcherService();
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal) { ["role"] = "admin" },
                }
            ]
        };

        var evaluations = matcher.EvaluateCandidates([], operation, Query(("role", "user")), Headers(), body: null);

        var mismatch = Assert.Single(evaluations[0].MismatchReasons);
        Assert.Equal("query", mismatch.Dimension);
        Assert.Equal("role", mismatch.Key);
        Assert.Equal("admin", mismatch.Expected);
        Assert.Equal("user", mismatch.Actual);
        Assert.Equal("unequal", mismatch.Kind);
    }

    [Fact]
    public void EvaluateCandidates_HeaderKeyMissing_EmitsMissingMismatch()
    {
        var matcher = CreateMatcherService();
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["X-Env"] = "staging" },
                }
            ]
        };

        var evaluations = matcher.EvaluateCandidates([], operation, Query(), Headers(), body: null);

        var mismatch = Assert.Single(evaluations[0].MismatchReasons);
        Assert.Equal("header", mismatch.Dimension);
        Assert.Equal("X-Env", mismatch.Key);
        Assert.Equal("staging", mismatch.Expected);
        Assert.Null(mismatch.Actual);
        Assert.Equal("missing", mismatch.Kind);
    }

    [Fact]
    public void EvaluateCandidates_HeaderKeyPresentButWrongValue_EmitsUnequalMismatch()
    {
        var matcher = CreateMatcherService();
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["X-Env"] = "staging" },
                }
            ]
        };

        var evaluations = matcher.EvaluateCandidates([], operation, Query(), Headers(("X-Env", "production")), body: null);

        var mismatch = Assert.Single(evaluations[0].MismatchReasons);
        Assert.Equal("header", mismatch.Dimension);
        Assert.Equal("X-Env", mismatch.Key);
        Assert.Equal("staging", mismatch.Expected);
        Assert.Equal("production", mismatch.Actual);
        Assert.Equal("unequal", mismatch.Kind);
    }

    [Fact]
    public void EvaluateCandidates_RegexQueryKeyMissing_EmitsMissingMismatch()
    {
        var matcher = CreateMatcherService();
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["token"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["regex"] = "^[A-Z]+" }
                    },
                }
            ]
        };

        var evaluations = matcher.EvaluateCandidates([], operation, Query(), Headers(), body: null);

        var mismatch = Assert.Single(evaluations[0].MismatchReasons);
        Assert.Equal("query", mismatch.Dimension);
        Assert.Equal("token", mismatch.Key);
        Assert.Equal("^[A-Z]+", mismatch.Expected);
        Assert.Null(mismatch.Actual);
        Assert.Equal("missing", mismatch.Kind);
    }

    [Fact]
    public void EvaluateCandidates_RegexQueryKeyPresentButNoMatch_EmitsUnequalMismatch()
    {
        var matcher = CreateMatcherService();
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["token"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["regex"] = "^[A-Z]+" }
                    },
                }
            ]
        };

        var evaluations = matcher.EvaluateCandidates([], operation, Query(("token", "lowercase")), Headers(), body: null);

        var mismatch = Assert.Single(evaluations[0].MismatchReasons);
        Assert.Equal("query", mismatch.Dimension);
        Assert.Equal("token", mismatch.Key);
        Assert.Equal("^[A-Z]+", mismatch.Expected);
        Assert.Equal("lowercase", mismatch.Actual);
        Assert.Equal("unequal", mismatch.Kind);
    }

    [Fact]
    public void EvaluateCandidates_MultipleMismatchedKeys_EmitsAllMismatches()
    {
        var matcher = CreateMatcherService();
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["role"] = "admin",
                        ["view"] = "full",
                    },
                }
            ]
        };

        var evaluations = matcher.EvaluateCandidates([], operation, Query(("role", "user")), Headers(), body: null);

        Assert.Equal(2, evaluations[0].MismatchReasons.Count);
        Assert.Contains(evaluations[0].MismatchReasons, m => m.Key == "role" && m.Kind == "unequal");
        Assert.Contains(evaluations[0].MismatchReasons, m => m.Key == "view" && m.Kind == "missing");
    }

    [Fact]
    public void EvaluateCandidates_MultiValueQueryExpected_SerializesExpectedAsJoinedString()
    {
        var matcher = CreateMatcherService();
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["tag"] = new List<object?> { "alpha", "beta" }
                    },
                }
            ]
        };

        var evaluations = matcher.EvaluateCandidates([], operation, Query(("tag", "gamma")), Headers(), body: null);

        var mismatch = Assert.Single(evaluations[0].MismatchReasons);
        Assert.Equal("query", mismatch.Dimension);
        Assert.Equal("tag", mismatch.Key);
        Assert.Equal("alpha, beta", mismatch.Expected);
        Assert.NotNull(mismatch.Actual);
        Assert.Equal("unequal", mismatch.Kind);
    }

    [Fact]
    public void EvaluateCandidates_MultiValueRegexExpected_SerializesExpectedAsJoinedString()
    {
        var matcher = CreateMatcherService();
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["token"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["regex"] = new List<object?> { "^A.*", "^B.*" }
                        }
                    },
                }
            ]
        };

        var evaluations = matcher.EvaluateCandidates([], operation, Query(("token", "lowercase")), Headers(), body: null);

        var mismatch = Assert.Single(evaluations[0].MismatchReasons);
        Assert.Equal("query", mismatch.Dimension);
        Assert.Equal("token", mismatch.Key);
        Assert.Equal("^A.*, ^B.*", mismatch.Expected);
        Assert.Equal("lowercase", mismatch.Actual);
        Assert.Equal("unequal", mismatch.Kind);
    }

    [Fact]
    public void EvaluateCandidates_JsonBodyMissingNestedProperty_EmitsBodyMissingMismatch()
    {
        var matcher = CreateMatcherService();
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Body = new Dictionary<object, object?>
                    {
                        ["user"] = new Dictionary<object, object?>
                        {
                            ["profile"] = new Dictionary<object, object?>
                            {
                                ["role"] = "admin"
                            }
                        }
                    },
                }
            ]
        };

        var evaluations = matcher.EvaluateCandidates(
            [],
            operation,
            Query(),
            Headers(),
            body: "{\"user\":{\"profile\":{}}}");

        var mismatch = Assert.Single(evaluations[0].MismatchReasons);
        Assert.Equal("body", mismatch.Dimension);
        Assert.Equal("$.user.profile.role", mismatch.Key);
        Assert.Equal("admin", mismatch.Expected);
        Assert.Null(mismatch.Actual);
        Assert.Equal("missing", mismatch.Kind);
    }

    [Fact]
    public void EvaluateCandidates_JsonBodyNestedValueDiffers_EmitsBodyUnequalMismatch()
    {
        var matcher = CreateMatcherService();
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Body = new Dictionary<object, object?>
                    {
                        ["user"] = new Dictionary<object, object?>
                        {
                            ["profile"] = new Dictionary<object, object?>
                            {
                                ["role"] = "admin"
                            }
                        }
                    },
                }
            ]
        };

        var evaluations = matcher.EvaluateCandidates(
            [],
            operation,
            Query(),
            Headers(),
            body: "{\"user\":{\"profile\":{\"role\":\"user\"}}}");

        var mismatch = Assert.Single(evaluations[0].MismatchReasons);
        Assert.Equal("body", mismatch.Dimension);
        Assert.Equal("$.user.profile.role", mismatch.Key);
        Assert.Equal("admin", mismatch.Expected);
        Assert.Equal("user", mismatch.Actual);
        Assert.Equal("unequal", mismatch.Kind);
    }

    [Fact]
    public void EvaluateCandidates_AllDimensionsMatch_EmptyMismatchReasons()
    {
        var matcher = CreateMatcherService();
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                {
                    Query = new Dictionary<string, object?>(StringComparer.Ordinal) { ["role"] = "admin" },
                    Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["X-Env"] = "staging" },
                }
            ]
        };

        var evaluations = matcher.EvaluateCandidates(
            [],
            operation,
            Query(("role", "admin")),
            Headers(("X-Env", "staging")),
            body: null);

        Assert.True(evaluations[0].QueryMatched);
        Assert.True(evaluations[0].HeaderMatched);
        Assert.Empty(evaluations[0].MismatchReasons);
    }
}
