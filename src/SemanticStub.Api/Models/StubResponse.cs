using Microsoft.Extensions.Primitives;

namespace SemanticStub.Api.Models;

/// <summary>
/// Represents the HTTP response contract produced by <see cref="Services.IStubService"/> after a stub match succeeds.
/// </summary>
public sealed class StubResponse
{
    private int _statusCode = 200;
    private int? _delayMilliseconds;
    private string _body = string.Empty;
    private string? _filePath;

    /// <summary>
    /// Gets the HTTP status code configured on the selected YAML response.
    /// </summary>
    public int StatusCode
    {
        get => _statusCode;
        init
        {
            if (value is < 100 or > 599)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "StatusCode must be between 100 and 599.");
            }

            _statusCode = value;
        }
    }

    /// <summary>
    /// Gets the optional response delay in milliseconds. <see langword="null"/> means "respond immediately".
    /// </summary>
    public int? DelayMilliseconds
    {
        get => _delayMilliseconds;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "DelayMilliseconds cannot be negative.");
            }

            _delayMilliseconds = value;
        }
    }

    /// <summary>
    /// Gets the response content type chosen from the YAML <c>content</c> section. Defaults to <c>application/json</c> when no explicit media type is available.
    /// </summary>
    public string ContentType { get; init; } = "application/json";

    /// <summary>
    /// Gets the response headers copied from the selected YAML response. Header names use case-insensitive lookup semantics.
    /// </summary>
    public IReadOnlyDictionary<string, StringValues> Headers { get; init; } = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the in-memory response body. This is empty when <see cref="FilePath"/> is used to stream a file-backed response instead.
    /// </summary>
    public string Body
    {
        get => _body;
        init
        {
            if (!string.IsNullOrEmpty(_filePath) && !string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException("Body and FilePath cannot both be set on StubResponse.");
            }

            _body = value;
        }
    }

    /// <summary>
    /// Gets the absolute file path to stream when the selected response is backed by a file on disk. <see langword="null"/> means the response should use <see cref="Body"/>.
    /// </summary>
    public string? FilePath
    {
        get => _filePath;
        init
        {
            if (!string.IsNullOrEmpty(_body) && !string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException("Body and FilePath cannot both be set on StubResponse.");
            }

            _filePath = value;
        }
    }
}
