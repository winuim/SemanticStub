using SemanticStub.Application.Models;
using SemanticStub.Application.Services;
using Xunit;

namespace SemanticStub.Application.Tests.Unit;

public sealed class QueryParameterTypeMapBuilderTests
{
    [Fact]
    public void Build_IncludesQueryParameterTypesFromPathAndOperationLevels()
    {
        var pathParameters = new[]
        {
            new ParameterDefinition
            {
                Name = "page",
                In = "query",
                Schema = new ParameterSchemaDefinition
                {
                    Type = "integer"
                }
            }
        };

        var operationParameters = new[]
        {
            new ParameterDefinition
            {
                Name = "enabled",
                In = "query",
                Schema = new ParameterSchemaDefinition
                {
                    Type = "boolean"
                }
            }
        };

        var result = QueryParameterTypeMapBuilder.Build(pathParameters, operationParameters);

        Assert.Equal("integer", result["page"]);
        Assert.Equal("boolean", result["enabled"]);
    }

    [Fact]
    public void Build_PrefersOperationLevelQueryTypeWhenNamesOverlap()
    {
        var pathParameters = new[]
        {
            new ParameterDefinition
            {
                Name = "page",
                In = "query",
                Schema = new ParameterSchemaDefinition
                {
                    Type = "string"
                }
            }
        };

        var operationParameters = new[]
        {
            new ParameterDefinition
            {
                Name = "page",
                In = "query",
                Schema = new ParameterSchemaDefinition
                {
                    Type = "integer"
                }
            }
        };

        var result = QueryParameterTypeMapBuilder.Build(pathParameters, operationParameters);

        Assert.Equal("integer", result["page"]);
    }
}
