using SemanticStub.Api.Models;
using SemanticStub.Api.Utilities;

namespace SemanticStub.Api.Infrastructure.Yaml;

internal sealed class StubDefinitionNormalizer
{
    public StubDocument NormalizeDocument(StubDocument document, string definitionDirectory)
    {
        return new StubDocument
        {
            OpenApi = document.OpenApi,
            Paths = document.Paths.ToDictionary(
                entry => entry.Key,
                entry => NormalizePathItem(entry.Value, definitionDirectory),
                StringComparer.Ordinal)
        };
    }

    private static PathItemDefinition NormalizePathItem(PathItemDefinition pathItem, string definitionDirectory)
    {
        return new PathItemDefinition
        {
            Parameters = [.. pathItem.Parameters.Select(parameter => new ParameterDefinition
            {
                Name = parameter.Name,
                In = parameter.In,
                Schema = parameter.Schema is null
                    ? null
                    : new ParameterSchemaDefinition
                    {
                        Type = parameter.Schema.Type
                    }
            })],
            Get = NormalizeOperation(pathItem.Get, definitionDirectory),
            Post = NormalizeOperation(pathItem.Post, definitionDirectory),
            Put = NormalizeOperation(pathItem.Put, definitionDirectory),
            Patch = NormalizeOperation(pathItem.Patch, definitionDirectory),
            Delete = NormalizeOperation(pathItem.Delete, definitionDirectory)
        };
    }

    private static OperationDefinition? NormalizeOperation(OperationDefinition? operation, string definitionDirectory)
    {
        if (operation is null)
        {
            return null;
        }

        return new OperationDefinition
        {
            OperationId = operation.OperationId,
            Parameters = [.. operation.Parameters.Select(parameter => new ParameterDefinition
            {
                Name = parameter.Name,
                In = parameter.In,
                Schema = parameter.Schema is null
                    ? null
                    : new ParameterSchemaDefinition
                    {
                        Type = parameter.Schema.Type
                    }
            })],
            Matches =
            [
                .. operation.Matches.Select(match => new QueryMatchDefinition
                {
                    Query = match.Query.ToDictionary(
                        entry => entry.Key,
                        entry => StubExampleSerializer.NormalizeValue(entry.Value),
                        StringComparer.Ordinal),
                    PartialQuery = match.PartialQuery.ToDictionary(
                        entry => entry.Key,
                        entry => StubExampleSerializer.NormalizeValue(entry.Value),
                        StringComparer.Ordinal),
                    Headers = new Dictionary<string, string>(match.Headers, StringComparer.OrdinalIgnoreCase),
                    Body = StubExampleSerializer.NormalizeValue(match.Body),
                    Response = new QueryMatchResponseDefinition
                    {
                        StatusCode = match.Response.StatusCode,
                        DelayMilliseconds = match.Response.DelayMilliseconds,
                        ResponseFile = StubDefinitionPathResolver.ResolveResponseFilePath(definitionDirectory, match.Response.ResponseFile),
                        Headers = new Dictionary<string, HeaderDefinition>(match.Response.Headers, StringComparer.OrdinalIgnoreCase),
                        Content = new Dictionary<string, MediaTypeDefinition>(match.Response.Content, StringComparer.Ordinal)
                    }
                })
            ],
            Responses = operation.Responses.ToDictionary(
                entry => entry.Key,
                entry => new ResponseDefinition
                {
                    Description = entry.Value.Description,
                    DelayMilliseconds = entry.Value.DelayMilliseconds,
                    ResponseFile = StubDefinitionPathResolver.ResolveResponseFilePath(definitionDirectory, entry.Value.ResponseFile),
                    Headers = new Dictionary<string, HeaderDefinition>(entry.Value.Headers, StringComparer.OrdinalIgnoreCase),
                    Content = new Dictionary<string, MediaTypeDefinition>(entry.Value.Content, StringComparer.Ordinal)
                },
                StringComparer.Ordinal)
        };
    }
}
