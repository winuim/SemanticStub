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

        Assert.True(matched);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal("{\"message\":\"Created\"}", response.Body);
    }
}
