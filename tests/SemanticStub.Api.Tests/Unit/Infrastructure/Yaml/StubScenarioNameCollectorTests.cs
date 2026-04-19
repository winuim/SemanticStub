using SemanticStub.Application.Infrastructure.Yaml;
using SemanticStub.Infrastructure.Yaml;
using SemanticStub.Api.Models;
using SemanticStub.Application.Models;
using Xunit;

namespace SemanticStub.Api.Tests.Unit.Infrastructure.Yaml;

public sealed class StubScenarioNameCollectorTests
{
    [Fact]
    public void Collect_ReturnsScenarioNamesFromResponsesAndConditionalMatches()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/checkout"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["409"] = new()
                            {
                                Scenario = new ScenarioDefinition
                                {
                                    Name = "checkout-flow",
                                    State = "initial",
                                    Next = "confirmed"
                                }
                            }
                        },
                        Matches =
                        [
                            new QueryMatchDefinition
                            {
                                Response = new QueryMatchResponseDefinition
                                {
                                    StatusCode = 200,
                                    Scenario = new ScenarioDefinition
                                    {
                                        Name = "payment-flow",
                                        State = "initial",
                                        Next = "authorized"
                                    }
                                }
                            }
                        ]
                    }
                }
            }
        };

        var scenarioNames = StubScenarioNameCollector.Collect(document);

        Assert.Equal(["checkout-flow", "payment-flow"], scenarioNames);
    }

    [Fact]
    public void Collect_DeduplicatesScenarioNamesWhilePreservingDiscoveryOrder()
    {
        var document = new StubDocument
        {
            Paths = new Dictionary<string, PathItemDefinition>(StringComparer.Ordinal)
            {
                ["/checkout"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Responses = new Dictionary<string, ResponseDefinition>(StringComparer.Ordinal)
                        {
                            ["409"] = new()
                            {
                                Scenario = new ScenarioDefinition
                                {
                                    Name = "checkout-flow",
                                    State = "initial",
                                    Next = "confirmed"
                                }
                            },
                            ["200"] = new()
                            {
                                Scenario = new ScenarioDefinition
                                {
                                    Name = "checkout-flow",
                                    State = "confirmed"
                                }
                            }
                        },
                        Matches =
                        [
                            new QueryMatchDefinition
                            {
                                Response = new QueryMatchResponseDefinition
                                {
                                    StatusCode = 202,
                                    Scenario = new ScenarioDefinition
                                    {
                                        Name = "checkout-flow",
                                        State = "confirmed"
                                    }
                                }
                            }
                        ]
                    }
                },
                ["/payment"] = new()
                {
                    Post = new OperationDefinition
                    {
                        Matches =
                        [
                            new QueryMatchDefinition
                            {
                                Response = new QueryMatchResponseDefinition
                                {
                                    StatusCode = 200,
                                    Scenario = new ScenarioDefinition
                                    {
                                        Name = "payment-flow",
                                        State = "initial",
                                        Next = "authorized"
                                    }
                                }
                            }
                        ]
                    }
                }
            }
        };

        var scenarioNames = StubScenarioNameCollector.Collect(document);

        Assert.Equal(["checkout-flow", "payment-flow"], scenarioNames);
    }
}
