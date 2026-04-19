using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SemanticStub.Api.Controllers;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Controllers;

public sealed class StubRequestBodyReaderTests
{
    [Fact]
    public async Task ReadAsync_WhenFormUrlEncoded_SerializesFormValues()
    {
        var request = CreateRequest("name=Ada%20Lovelace&tag=alpha&tag=beta&empty=", "application/x-www-form-urlencoded; charset=utf-8");

        var body = await StubRequestBodyReader.ReadAsync(request, NullLogger.Instance);

        Assert.Equal("name=Ada%20Lovelace&tag=alpha&tag=beta&empty=", body);
    }

    [Fact]
    public async Task ReadAsync_WhenFormIsEmpty_ReturnsNull()
    {
        var request = CreateRequest(string.Empty, "application/x-www-form-urlencoded");

        var body = await StubRequestBodyReader.ReadAsync(request, NullLogger.Instance);

        Assert.Null(body);
    }

    [Fact]
    public async Task ReadAsync_WhenBodyIsUnreadable_ReturnsNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new ThrowingReadStream();
        var logger = new ListLogger();

        var body = await StubRequestBodyReader.ReadAsync(context.Request, logger);

        Assert.Null(body);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.LogLevel);
        Assert.IsType<IOException>(entry.Exception);
    }

    [Fact]
    public async Task ReadAsync_WhenBodyIsSeekable_ResetsStreamPosition()
    {
        var request = CreateRequest("{\"username\":\"demo\"}", "application/json");

        var body = await StubRequestBodyReader.ReadAsync(request, NullLogger.Instance);

        Assert.Equal("{\"username\":\"demo\"}", body);
        Assert.Equal(0, request.Body.Position);
    }

    private static HttpRequest CreateRequest(string body, string contentType)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentLength = context.Request.Body.Length;
        return context.Request;
    }

    private sealed class ThrowingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Simulated disconnect.");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("Simulated disconnect."));

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ListLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, exception));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, Exception? Exception);

}
