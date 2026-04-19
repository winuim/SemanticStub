using SemanticStub.Api.Models;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Models;

public sealed class StubResponseTests
{
    [Fact]
    public void Constructor_DefaultsToSuccessfulInMemoryResponseShape()
    {
        var response = new StubResponse();

        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.DelayMilliseconds);
        Assert.Equal(string.Empty, response.Body);
        Assert.Null(response.FilePath);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(600)]
    public void StatusCode_ThrowsWhenOutsideHttpRange(int statusCode)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StubResponse { StatusCode = statusCode });
    }

    [Fact]
    public void DelayMilliseconds_ThrowsWhenNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StubResponse { DelayMilliseconds = -1 });
    }

    [Fact]
    public void FilePath_ThrowsWhenBodyAlreadySet()
    {
        Assert.Throws<InvalidOperationException>(() => new StubResponse
        {
            Body = "payload",
            FilePath = "/tmp/file.json"
        });
    }

    [Fact]
    public void Body_ThrowsWhenFilePathAlreadySet()
    {
        Assert.Throws<InvalidOperationException>(() => new StubResponse
        {
            FilePath = "/tmp/file.json",
            Body = "payload"
        });
    }
}
