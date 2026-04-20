using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using SemanticStub.Application.Infrastructure.Yaml;
using SemanticStub.Application.Models;
using SemanticStub.Application.Services.Semantic;
using SemanticStub.Infrastructure.Semantic;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Semantic;

public sealed class SemanticMatcherServiceTests
{
    [Fact]
    public async Task ExplainMatchAsync_ReturnsNullSelectedCandidateWhenSemanticMatchingIsDisabled()
    {
        var service = CreateService(
            new StubSettings(),
            (_, _) => throw new InvalidOperationException("The HTTP client should not be called when semantic matching is disabled."));

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users")]);

        Assert.Null(explanation.SelectedCandidate);
    }

    [Fact]
    public async Task ExplainMatchAsync_ReturnsUnattemptedExplanationWhenNoSemanticCandidatesAreEligible()
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
            (_, _) => throw new InvalidOperationException("The HTTP client should not be called when there are no semantic candidates."));

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [new QueryMatchDefinition()]);

        Assert.False(explanation.Attempted);
        Assert.Null(explanation.SelectedCandidate);
        Assert.Empty(explanation.CandidateScores);
    }

    [Fact]
    public async Task ExplainMatchAsync_ReturnsHighestScoringCandidateAboveThreshold()
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

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [adminCandidate, invoiceCandidate]);

        Assert.Same(adminCandidate, explanation.SelectedCandidate);
    }

    [Fact]
    public async Task ExplainMatchAsync_ReturnsAttemptedNonMatchWhenEmbeddingCallFails()
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
            (_, _) => throw new HttpRequestException("boom"));

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users")]);

        Assert.True(explanation.Attempted);
        Assert.Null(explanation.SelectedCandidate);
        Assert.Equal(0.8d, explanation.Threshold);
        Assert.Equal(0.03d, explanation.RequiredMargin);
        Assert.Empty(explanation.CandidateScores);
    }

    [Fact]
    public async Task ExplainMatchAsync_ReturnsAttemptedNonMatchWhenEmbeddingResponseIsInvalidJson()
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
            (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-valid-json", Encoding.UTF8, "application/json")
            });

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users")]);

        Assert.True(explanation.Attempted);
        Assert.Null(explanation.SelectedCandidate);
        Assert.Equal(0.8d, explanation.Threshold);
        Assert.Equal(0.03d, explanation.RequiredMargin);
    }

    [Fact]
    public async Task ExplainMatchAsync_PropagatesUnexpectedExceptions()
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
            (_, _) => throw new ArgumentNullException("Unexpected bug"));

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users")]));
    }

    [Fact]
    public async Task ExplainMatchAsync_ReturnsNullSelectedCandidateWhenBestScoreIsBelowThreshold()
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

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users")]);

        Assert.Null(explanation.SelectedCandidate);
    }

    [Fact]
    public async Task ExplainMatchAsync_UsesDefaultThresholdWhenNoneIsConfigured()
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
                ["method: POST\npath: /search\nbody:\ncoffee shop search"] = "[1.0,0.0]",
                ["show unpaid billing invoices"] = "[0.84,0.5425863986500215]"
            }));

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "coffee shop search",
            [CreateCandidate("show unpaid billing invoices")]);

        Assert.Null(explanation.SelectedCandidate);
    }

    [Fact]
    public async Task ExplainMatchAsync_ReturnsScoresAndSelectedCandidate()
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

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [adminCandidate, invoiceCandidate],
            includeCandidateScores: true);

        Assert.True(explanation.Attempted);
        Assert.Same(adminCandidate, explanation.SelectedCandidate);
        Assert.Equal(0.8d, explanation.Threshold);
        Assert.Equal(2, explanation.CandidateScores.Count);
        Assert.Contains(explanation.CandidateScores, score => ReferenceEquals(score.Candidate, adminCandidate) && score.AboveThreshold);
    }

    [Fact]
    public async Task ExplainMatchAsync_ReturnsSelectionMetadataWhenTopScoreMarginIsTooSmall()
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

        var firstCandidate = CreateCandidate("find admin users");
        var secondCandidate = CreateCandidate("find administrator accounts");

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [firstCandidate, secondCandidate],
            includeCandidateScores: true);

        Assert.True(explanation.Attempted);
        Assert.Null(explanation.SelectedCandidate);
        Assert.NotNull(explanation.SelectedScore);
        Assert.NotNull(explanation.SecondBestScore);
        Assert.NotNull(explanation.MarginToSecondBest);
        Assert.Equal(2, explanation.CandidateScores.Count);
    }

    [Fact]
    public async Task ExplainMatchAsync_DoesNotReturnCandidateScoresWhenNotRequested()
    {
        var service = CreateService(
            new StubSettings
            {
                SemanticMatching = new SemanticMatchingSettings
                {
                    Enabled = true,
                    Endpoint = "http://tei",
                    Threshold = 0.8d
                }
            },
            CreateEmbeddingHandler(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["method: POST\npath: /search\nbody:\nadmin search"] = "[1.0,0.0]",
                ["find admin users"] = "[0.95,0.05]"
            }));

        var candidate = CreateCandidate("find admin users");

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [candidate],
            includeCandidateScores: false);

        Assert.True(explanation.Attempted);
        Assert.Same(candidate, explanation.SelectedCandidate);
        Assert.Empty(explanation.CandidateScores);
    }

    [Fact]
    public async Task ExplainMatchAsync_ReturnsNullSelectedCandidateWhenEmbeddingVectorHasZeroMagnitude()
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
            (_, _) => CreateEmbeddingResponse("[[0.0,0.0],[0.9,0.1]]"));

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users")]);

        Assert.Null(explanation.SelectedCandidate);
    }

    [Fact]
    public async Task ExplainMatchAsync_ReturnsNullSelectedCandidateWhenEmbeddingVectorDimensionsDoNotMatch()
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
            (_, _) => CreateEmbeddingResponse("[[1.0,0.0],[0.9,0.1,0.2]]"));

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users")]);

        Assert.Null(explanation.SelectedCandidate);
    }

    [Theory]
    [InlineData("http://tei", "http://tei/embed")]
    [InlineData("http://tei/", "http://tei/embed")]
    [InlineData("http://tei/embed", "http://tei/embed")]
    [InlineData("http://tei/embed/", "http://tei/embed")]
    public async Task ExplainMatchAsync_NormalizesEmbeddingEndpointWithoutChangingBehavior(string configuredEndpoint, string expectedEndpoint)
    {
        Uri? actualRequestUri = null;

        var service = CreateService(
            new StubSettings
            {
                SemanticMatching = new SemanticMatchingSettings
                {
                    Enabled = true,
                    Endpoint = configuredEndpoint
                }
            },
            (request, _) =>
            {
                actualRequestUri = request.RequestUri;
                return CreateEmbeddingResponse("[[1.0,0.0],[0.9,0.1]]");
            });

        var candidate = CreateCandidate("find admin users");

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [candidate]);

        Assert.Same(candidate, explanation.SelectedCandidate);
        Assert.Equal(expectedEndpoint, actualRequestUri?.ToString());
    }

    [Fact]
    public async Task ExplainMatchAsync_BuildsRequestTextFromMethodPathQueryHeadersAndTrimmedBody()
    {
        string? capturedRequestText = null;

        var service = CreateService(
            new StubSettings
            {
                SemanticMatching = new SemanticMatchingSettings
                {
                    Enabled = true,
                    Endpoint = "http://tei"
                }
            },
            (request, _) =>
            {
                using var document = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                capturedRequestText = document.RootElement.GetProperty("inputs")[0].GetString();
                return CreateEmbeddingResponse("[[1.0,0.0],[0.9,0.1]]");
            });

        var candidate = CreateCandidate("find admin users");

        var explanation = await service.ExplainMatchAsync(
            "post",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["z"] = new StringValues(["last", "value"]),
                ["a"] = new StringValues("first")
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-tenant"] = "tenant-a",
                ["Accept"] = "application/json"
            },
            "  admin search  ",
            [candidate]);

        Assert.Same(candidate, explanation.SelectedCandidate);
        Assert.Equal(
            "method: POST\npath: /search\nquery:\n  a: first\n  z: last, value\nheaders:\n  Accept: application/json\n  x-tenant: tenant-a\nbody:\nadmin search",
            capturedRequestText);
    }

    [Fact]
    public async Task ExplainMatchAsync_ReturnsNullSelectedCandidateWhenEmbeddingResponseShapeIsUnexpected()
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
            (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"embeddings\":[[1.0,0.0]]}", Encoding.UTF8, "application/json")
            });

        var explanation = await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users")]);

        Assert.Null(explanation.SelectedCandidate);
    }

    [Fact]
    public async Task ExplainMatchAsync_PropagatesCancellationToken_ToEmbeddingClient()
    {
        CancellationToken receivedToken = default;
        using var cancellationSource = new CancellationTokenSource();

        var service = CreateService(
            new StubSettings
            {
                SemanticMatching = new SemanticMatchingSettings
                {
                    Enabled = true,
                    Endpoint = "http://tei"
                }
            },
            (_, ct) =>
            {
                receivedToken = ct;
                return CreateEmbeddingResponse("[[1.0,0.0],[0.9,0.1]]");
            });

        await service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users")],
            cancellationToken: cancellationSource.Token);

        // HttpClient links the caller's token internally, so the received token is a linked token rather
        // than the original. Verify that a cancellable token (not CancellationToken.None) was forwarded.
        Assert.True(receivedToken.CanBeCanceled);
    }

    [Fact]
    public async Task ExplainMatchAsync_ThrowsOperationCanceledException_WhenTokenCancelledDuringEmbeddingCall()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var service = CreateService(
            new StubSettings
            {
                SemanticMatching = new SemanticMatchingSettings
                {
                    Enabled = true,
                    Endpoint = "http://tei"
                }
            },
            (_, _) => CreateEmbeddingResponse("[[1.0,0.0],[0.9,0.1]]"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExplainMatchAsync(
            "POST",
            "/search",
            new Dictionary<string, StringValues>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "admin search",
            [CreateCandidate("find admin users")],
            cancellationToken: cancellationSource.Token));
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
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(settings));
        services.AddLogging();
        services.AddSingleton<IHttpClientFactory>(_ => new TestHttpClientFactory(new HttpClient(new DelegatingTestHandler(handler))));
        services.AddSingleton<ISemanticEmbeddingClient, SemanticEmbeddingClient>();
        services.AddSingleton<ISemanticMatcherService>(serviceProvider => new SemanticMatcherService(
            serviceProvider.GetRequiredService<ISemanticEmbeddingClient>(),
            serviceProvider.GetRequiredService<IOptions<StubSettings>>().Value,
            serviceProvider.GetRequiredService<ILogger<SemanticMatcherService>>()));

        return (SemanticMatcherService)services.BuildServiceProvider().GetRequiredService<ISemanticMatcherService>();
    }

    private static Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> CreateEmbeddingHandler(
        IReadOnlyDictionary<string, string> embeddingsByInput)
    {
        return (request, _) =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            using var document = JsonDocument.Parse(body);
            var inputs = document.RootElement.GetProperty("inputs")
                .EnumerateArray()
                .Select(e => e.GetString()!)
                .ToArray();

            var results = new List<string>();

            foreach (var input in inputs)
            {
                if (!embeddingsByInput.TryGetValue(input, out var embedding))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                results.Add(embedding);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"[{string.Join(",", results)}]", Encoding.UTF8, "application/json")
            };
        };
    }

    private static HttpResponseMessage CreateEmbeddingResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
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

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return client;
        }
    }
}
