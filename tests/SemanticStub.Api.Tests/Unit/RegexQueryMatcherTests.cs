using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class RegexQueryMatcherTests
{
    [Fact]
    public void IsMatch_MatchesSingleRegexQueryValue()
    {
        var matcher = new RegexQueryMatcher();

        var matched = matcher.IsMatch(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = "^admin-[0-9]+$"
            },
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["role"] = new StringValues("admin-42")
            });

        Assert.True(matched);
    }

    [Fact]
    public void IsMatch_MatchesRepeatedRegexQueryValuesInOrder()
    {
        var matcher = new RegexQueryMatcher();

        var matched = matcher.IsMatch(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tag"] = new List<object?> { "^alpha$", "^beta$" }
            },
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["tag"] = new StringValues(["alpha", "beta"])
            });

        Assert.True(matched);
    }

    [Fact]
    public void IsMatch_ReturnsFalseForInvalidRegexAndLogsWarning()
    {
        var logger = new ListLogger<RegexQueryMatcher>();
        var matcher = new RegexQueryMatcher(logger);

        var matched = matcher.IsMatch(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = "["
            },
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["role"] = new StringValues("admin")
            });

        Assert.False(matched);
        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, logger.Entries[0].LogLevel);
        Assert.Contains("Invalid x-regex-query pattern", logger.Entries[0].Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<ArgumentException>(logger.Entries[0].Exception);
    }

    [Fact]
    public void IsMatch_ReturnsFalseWhenRegexEvaluationTimesOutAndLogsWarning()
    {
        var logger = new ListLogger<RegexQueryMatcher>();
        var matcher = new RegexQueryMatcher(logger);

        var matched = matcher.IsMatch(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = "^(a+)+$"
            },
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["role"] = new StringValues(new string('a', 4096) + "!")
            });

        Assert.False(matched);
        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, logger.Entries[0].LogLevel);
        Assert.Contains("timed out after", logger.Entries[0].Message, StringComparison.Ordinal);
        Assert.Null(logger.Entries[0].Exception);
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
