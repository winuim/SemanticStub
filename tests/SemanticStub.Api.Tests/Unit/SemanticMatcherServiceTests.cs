using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class SemanticMatcherServiceTests
{
    [Fact]
    public async Task FindBestMatchAsync_ReturnsNullWhenSemanticMatchingIsDisabled()
    {
        var service = CreateService(
            new StubSettings(),
            (_, _) => throw new InvalidOperationException("The HTTP client should not be called when semantic matching is disabled."));

        var match = await service.FindBestMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users")]);

        Assert.Null(match);
    }

    [Fact]
    public async Task FindBestMatchAsync_ReturnsHighestScoringCandidateAboveThreshold()
    {
        var service = CreateService(
            new StubSettings
            {
                SemanticMatching = new SemanticMatchingSettings
                {
                    Enabled = true,
                    Endpoint = "http://tei"
                }
            },
            CreateEmbeddingHandler(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["method: POST\npath: /search\nbody:\nadmin search"] = "[1.0,0.0]",
                ["find admin users"] = "[0.9,0.1]",
                ["show invoices"] = "[0.0,1.0]"
            }));

        var adminCandidate = CreateCandidate("find admin users");
        var invoiceCandidate = CreateCandidate("show invoices");

        var match = await service.FindBestMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [adminCandidate, invoiceCandidate]);

        Assert.Same(adminCandidate, match);
    }

    [Fact]
    public async Task FindBestMatchAsync_ReturnsNullWhenEmbeddingCallFails()
    {
        var service = CreateService(
            new StubSettings
            {
                SemanticMatching = new SemanticMatchingSettings
                {
                    Enabled = true,
                    Endpoint = "http://tei"
                }
            },
            (_, _) => throw new HttpRequestException("boom"));

        var match = await service.FindBestMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users")]);

        Assert.Null(match);
    }

    [Fact]
    public async Task FindBestMatchAsync_ReturnsNullWhenBestScoreIsBelowThreshold()
    {
        var service = CreateService(
            new StubSettings
            {
                SemanticMatching = new SemanticMatchingSettings
                {
                    Enabled = true,
                    Endpoint = "http://tei",
                    Threshold = 0.999d
                }
            },
            CreateEmbeddingHandler(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["method: POST\npath: /search\nbody:\nadmin search"] = "[1.0,0.0]",
                ["find admin users"] = "[0.9,0.1]"
            }));

        var match = await service.FindBestMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users")]);

        Assert.Null(match);
    }

    [Fact]
    public async Task FindBestMatchAsync_ReturnsNullWhenTopScoreMarginIsTooSmall()
    {
        var service = CreateService(
            new StubSettings
            {
                SemanticMatching = new SemanticMatchingSettings
                {
                    Enabled = true,
                    Endpoint = "http://tei",
                    Threshold = 0.8d,
                    TopScoreMargin = 0.03d
                }
            },
            CreateEmbeddingHandler(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["method: POST\npath: /search\nbody:\nadmin search"] = "[1.0,0.0]",
                ["find admin users"] = "[0.95,0.05]",
                ["find administrator accounts"] = "[0.93,0.07]"
            }));

        var match = await service.FindBestMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users"), CreateCandidate("find administrator accounts")]);

        Assert.Null(match);
    }

    [Fact]
    public async Task FindBestMatchAsync_ReturnsBestCandidateWhenTopScoreMarginIsSatisfied()
    {
        var service = CreateService(
            new StubSettings
            {
                SemanticMatching = new SemanticMatchingSettings
                {
                    Enabled = true,
                    Endpoint = "http://tei",
                    Threshold = 0.8d,
                    TopScoreMargin = 0.03d
                }
            },
            CreateEmbeddingHandler(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["method: POST\npath: /search\nbody:\nadmin search"] = "[1.0,0.0]",
                ["find admin users"] = "[0.95,0.05]",
                ["show invoices"] = "[0.60,0.40]"
            }));

        var adminCandidate = CreateCandidate("find admin users");
        var invoiceCandidate = CreateCandidate("show invoices");

        var match = await service.FindBestMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [adminCandidate, invoiceCandidate]);

        Assert.Same(adminCandidate, match);
    }

    private static QueryMatchDefinition CreateCandidate(string semanticMatch)
    {
        return new QueryMatchDefinition
        {
            SemanticMatch = semanticMatch,
            Response = new QueryMatchResponseDefinition
            {
                StatusCode = 200,
                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                {
                    ["application/json"] = new()
                    {
                        Example = new Dictionary<object, object>
                        {
                            ["message"] = semanticMatch
                        }
                    }
                }
            }
        };
    }

    private static SemanticMatcherService CreateService(
        StubSettings settings,
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new DelegatingTestHandler(handler));
        return new SemanticMatcherService(
            httpClient,
            Options.Create(settings),
            LoggerFactory.Create(_ => { }).CreateLogger<SemanticMatcherService>());
    }

    private static Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> CreateEmbeddingHandler(
        IReadOnlyDictionary<string, string> embeddingsByInput)
    {
        return (request, _) =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            using var document = JsonDocument.Parse(body);
            var input = document.RootElement.GetProperty("inputs").GetString()
                ?? throw new InvalidOperationException("The embedding request must contain an inputs value.");

            foreach (var pair in embeddingsByInput)
            {
                if (string.Equals(input, pair.Key, StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(pair.Value, Encoding.UTF8, "application/json")
                    };
                }
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
    }

    private sealed class DelegatingTestHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request, cancellationToken));
        }
    }
}
