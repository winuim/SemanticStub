using Microsoft.Extensions.Primitives;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services;
using System.Diagnostics;
using Xunit;

namespace SemanticStub.Application.Tests.Unit;

public sealed class MatcherBodyMatchingTests
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

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
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

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "{not-json");

        Assert.Null(match);
    }

    [Fact]
    public void FindBestMatch_MatchesFormUrlEncodedBodyCondition()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                    {
                        Body = new Dictionary<object, object>
                        {
                            ["form"] = new Dictionary<object, object>
                            {
                                ["userId"] = "test001",
                                ["password"] = "secret"
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
                ["Content-Type"] = "application/x-www-form-urlencoded"
            },
            "userId=test001&password=secret&extra=allowed");

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_MatchesFormUrlEncodedBodyRegexOperatorCondition()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                    {
                        Body = new Dictionary<object, object>
                        {
                            ["form"] = new Dictionary<object, object>
                            {
                                ["userId"] = new Dictionary<object, object>
                                {
                                    ["regex"] = "^[0-9]{6}$"
                                }
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
                ["Content-Type"] = "application/x-www-form-urlencoded"
            },
            "userId=123456&extra=allowed");

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_ReturnsNullWhenFormContentTypeIsMissing()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                    {
                        Body = new Dictionary<object, object>
                        {
                            ["form"] = new Dictionary<object, object>
                            {
                                ["userId"] = "test001"
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
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "userId=test001");

        Assert.Null(match);
    }

    [Fact]
    public void FindBestMatch_ReturnsNullWhenFormValueDiffers()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                    {
                        Body = new Dictionary<object, object>
                        {
                            ["form"] = new Dictionary<object, object>
                            {
                                ["userId"] = "test001"
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
                ["Content-Type"] = "application/x-www-form-urlencoded"
            },
            "userId=other");

        Assert.Null(match);
    }

    [Fact]
    public void FindBestMatch_KeepsBodyFormAsJsonWhenContentTypeIsJson()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                    {
                        Body = new Dictionary<object, object>
                        {
                            ["form"] = "legacy-json-property"
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
                ["Content-Type"] = "application/json"
            },
            "{\"form\":\"legacy-json-property\"}");

        Assert.NotNull(match);
    }

    [Fact]
    public void FindBestMatch_ReturnsNullWhenBodyDefinitionContainsUnsupportedValue()
    {
        var operation = new OperationDefinition
        {
            Matches =
            [
                new QueryMatchDefinition
                    {
                        Body = new Dictionary<object, object>
                        {
                            ["value"] = new IntPtr(42)
                        }
                    }
            ]
        };

        var matcher = CreateMatcherService();

        var match = FindBestMatch(
            matcher,
            operation,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "{\"value\":42}");

        Assert.Null(match);
    }
}
