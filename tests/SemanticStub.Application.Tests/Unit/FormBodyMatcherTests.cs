using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SemanticStub.Application.Services;
using Xunit;

namespace SemanticStub.Application.Tests.Unit;

public sealed class FormBodyMatcherTests
{
    [Fact]
    public void ParseRequestBody_DecodesFormValues()
    {
        var matcher = new FormBodyMatcher();

        var form = matcher.ParseRequestBody(
            "userId=test001&display+name=Ada+Lovelace&city=New%20York",
            "application/x-www-form-urlencoded; charset=utf-8");

        Assert.NotNull(form);
        Assert.Equal("test001", form["userId"].ToString());
        Assert.Equal("Ada Lovelace", form["display name"].ToString());
        Assert.Equal("New York", form["city"].ToString());
    }

    [Fact]
    public void ParseRequestBody_PreservesEmptyKeyOnlyAndRepeatedValues()
    {
        var matcher = new FormBodyMatcher();

        var form = matcher.ParseRequestBody("empty=&keyOnly&tag=a&tag=b", "application/x-www-form-urlencoded");

        Assert.NotNull(form);
        Assert.Equal(string.Empty, form["empty"].ToString());
        Assert.Equal(string.Empty, form["keyOnly"].ToString());
        Assert.Equal(new[] { "a", "b" }, form["tag"].ToArray());
    }

    [Fact]
    public void IsMatch_AllowsAdditionalRequestKeys()
    {
        var matcher = new FormBodyMatcher();
        var form = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["userId"] = "test001",
            ["password"] = "secret",
            ["extra"] = "allowed"
        };

        var matched = matcher.IsMatch(
            new Dictionary<object, object>
            {
                ["form"] = new Dictionary<object, object>
                {
                    ["userId"] = "test001",
                    ["password"] = "secret"
                }
            },
            form);

        Assert.True(matched);
    }

    [Fact]
    public void IsMatch_MatchesEqualsOperatorFormValue()
    {
        var matcher = new FormBodyMatcher();
        var form = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["userId"] = "test001"
        };

        var matched = matcher.IsMatch(
            new Dictionary<object, object>
            {
                ["form"] = new Dictionary<object, object>
                {
                    ["userId"] = new Dictionary<object, object>
                    {
                        ["equals"] = "test001"
                    }
                }
            },
            form);

        Assert.True(matched);
    }

    [Fact]
    public void IsMatch_MatchesRegexOperatorFormValue()
    {
        var matcher = new FormBodyMatcher();
        var form = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["code"] = "abc_123"
        };

        var matched = matcher.IsMatch(
            new Dictionary<object, object>
            {
                ["form"] = new Dictionary<object, object>
                {
                    ["code"] = new Dictionary<object, object>
                    {
                        ["regex"] = "^[A-Za-z0-9_]+$"
                    }
                }
            },
            form);

        Assert.True(matched);
    }

    [Fact]
    public void IsMatch_ReturnsFalseWhenRegexOperatorFormValueDiffers()
    {
        var matcher = new FormBodyMatcher();
        var form = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["userId"] = "abc123"
        };

        var matched = matcher.IsMatch(
            new Dictionary<object, object>
            {
                ["form"] = new Dictionary<object, object>
                {
                    ["userId"] = new Dictionary<object, object>
                    {
                        ["regex"] = "^[0-9]{6}$"
                    }
                }
            },
            form);

        Assert.False(matched);
    }

    [Fact]
    public void IsMatch_ReturnsFalseForInvalidRegexAndLogsWarning()
    {
        var logger = new ListLogger<FormBodyMatcher>();
        var matcher = new FormBodyMatcher(logger);
        var form = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["code"] = "abc_123"
        };

        var matched = matcher.IsMatch(
            new Dictionary<object, object>
            {
                ["form"] = new Dictionary<object, object>
                {
                    ["code"] = new Dictionary<object, object>
                    {
                        ["regex"] = "["
                    }
                }
            },
            form);

        Assert.False(matched);
        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, logger.Entries[0].LogLevel);
        Assert.Contains("Invalid regex match pattern", logger.Entries[0].Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<ArgumentException>(logger.Entries[0].Exception);
    }

    [Fact]
    public void IsMatch_ReturnsFalseWhenRegexEvaluationTimesOutAndLogsWarning()
    {
        var logger = new ListLogger<FormBodyMatcher>();
        var matcher = new FormBodyMatcher(logger);
        var form = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["code"] = new string('a', 4096) + "!"
        };

        var matched = matcher.IsMatch(
            new Dictionary<object, object>
            {
                ["form"] = new Dictionary<object, object>
                {
                    ["code"] = new Dictionary<object, object>
                    {
                        ["regex"] = "^(a+)+$"
                    }
                }
            },
            form);

        Assert.False(matched);
        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, logger.Entries[0].LogLevel);
        Assert.Contains("timed out after", logger.Entries[0].Message, StringComparison.Ordinal);
        Assert.Null(logger.Entries[0].Exception);
    }

    [Fact]
    public void IsMatch_RequiresRepeatedValuesToMatchInOrder()
    {
        var matcher = new FormBodyMatcher();
        var form = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["tag"] = new StringValues(["a", "b"])
        };

        var matched = matcher.IsMatch(
            new Dictionary<object, object>
            {
                ["form"] = new Dictionary<object, object>
                {
                    ["tag"] = new[] { "a", "b" }
                }
            },
            form);

        Assert.True(matched);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
