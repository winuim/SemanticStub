using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace SemanticStub.Api.Tests.Integration;

public sealed class HttpLoggingTests
{
    [Theory]
    [InlineData("Development", 4096)]
    [InlineData("Production", 1024)]
    public void CreateClient_ConfiguresBodyLogLimitsPerEnvironment(string environmentName, int expectedLimit)
    {
        using var factory = new HttpLoggingFactory(environmentName);
        using var client = factory.CreateClient();

        var options = factory.Services.GetRequiredService<IOptions<HttpLoggingOptions>>().Value;

        Assert.Equal(expectedLimit, options.RequestBodyLogLimit);
        Assert.Equal(expectedLimit, options.ResponseBodyLogLimit);
        Assert.True(options.LoggingFields.HasFlag(HttpLoggingFields.RequestBody));
        Assert.True(options.LoggingFields.HasFlag(HttpLoggingFields.ResponseBody));
    }

    [Fact]
    public async Task GetHello_WritesHttpLoggingEntries()
    {
        using var workspace = HttpLoggingWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /hello:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        application/json:
                          example:
                            message: hello
            """);
        var sink = new TestLoggerProvider();

        using var factory = new HttpLoggingFactory("Development", sink, workspace.RootPath);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/hello");

        response.EnsureSuccessStatusCode();

        Assert.Contains(
            sink.Entries,
            entry => entry.Category.Contains("HttpLoggingMiddleware", StringComparison.Ordinal) &&
                     entry.Message.Contains("/hello", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateClient_ConfiguresHttpLoggingCategoryAtInformation()
    {
        using var factory = new HttpLoggingFactory("Production");
        using var client = factory.CreateClient();

        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal("Information", configuration["Logging:LogLevel:Microsoft.AspNetCore.HttpLogging"]);
    }

    [Fact]
    public void CreateClient_ConfiguresYamlInfrastructureCategoryAtInformation()
    {
        using var factory = new HttpLoggingFactory("Production");
        using var client = factory.CreateClient();

        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal("Information", configuration["Logging:LogLevel:SemanticStub.Api.Infrastructure.Yaml"]);
    }

    [Fact]
    public async Task GetPlainText_WritesTextResponseBodyToHttpLogs()
    {
        using var workspace = HttpLoggingWorkspace.Create(
            """
            openapi: 3.1.0
            paths:
              /plain-text:
                get:
                  responses:
                    "200":
                      description: ok
                      content:
                        text/plain:
                          example: plain text response
            """);
        var sink = new TestLoggerProvider();

        using var factory = new HttpLoggingFactory("Development", sink, workspace.RootPath);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/plain-text");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            sink.Entries,
            entry => entry.Category.Contains("HttpLoggingMiddleware", StringComparison.Ordinal) &&
                     entry.Message.Contains("plain text response", StringComparison.Ordinal));
    }

    private sealed class HttpLoggingFactory(
        string environmentName,
        TestLoggerProvider? loggerProvider = null,
        string? contentRootPath = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environmentName);

            if (!string.IsNullOrEmpty(contentRootPath))
            {
                builder.UseContentRoot(contentRootPath);
            }

            if (loggerProvider is null)
            {
                return;
            }

            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddProvider(loggerProvider);
            });
        }
    }

    private sealed class HttpLoggingWorkspace(string rootPath) : IDisposable
    {
        public string RootPath { get; } = rootPath;

        public static HttpLoggingWorkspace Create(string yaml)
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "semanticstub-http-logging-tests", Guid.NewGuid().ToString("N"));
            var samplesPath = Path.Combine(rootPath, "samples");
            Directory.CreateDirectory(samplesPath);
            File.WriteAllText(Path.Combine(samplesPath, "basic-routing.yaml"), yaml);

            return new HttpLoggingWorkspace(rootPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        private readonly List<LogEntry> entries = [];

        public IReadOnlyList<LogEntry> Entries => entries;

        public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, entries);

        public void Dispose()
        {
        }
    }

    private sealed record LogEntry(string Category, string Message);

    private sealed class TestLogger(string categoryName, List<LogEntry> entries) : ILogger
    {
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
            entries.Add(new LogEntry(categoryName, formatter(state, exception)));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
