using Microsoft.AspNetCore.Http;
using SemanticStub.Api.Models;
using SemanticStub.Api.Services;
using Xunit;

namespace SemanticStub.Api.Tests.Unit;

public sealed class StubServiceTests
{
    [Fact]
    public void TryGetResponse_UsesStatusCodeDefinedInYamlResponses()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/hello"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["201"] = new()
                            {
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new()
                                    {
                                        Example = new Dictionary<object, object>
                                        {
                                            ["message"] = "Created"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var service = new StubService(document);

        var matched = service.TryGetResponse(HttpMethods.Get, "/hello", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal("{\"message\":\"Created\"}", response.Body);
    }

    [Fact]
    public void TryGetResponse_UsesResponseFileContentWhenConfigured()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/users"] = new()
                {
                    Get = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["200"] = new()
                            {
                                ResponseFile = "users.json",
                                Content = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal)
                                {
                                    ["application/json"] = new()
                                }
                            }
                        }
                    }
                }
            }
        };

        var service = new StubService(document, _ => "{\"users\":[{\"id\":1,\"name\":\"Alice\"}]}");

        var matched = service.TryGetResponse(HttpMethods.Get, "/users", out var response);

        Assert.Equal(StubMatchResult.Matched, matched);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("{\"users\":[{\"id\":1,\"name\":\"Alice\"}]}", response.Body);
    }
}
