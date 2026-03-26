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
            Get = NormalizeOperation(pathItem.Get, definitionDirectory),
            Post = NormalizeOperation(pathItem.Post, definitionDirectory)
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
            Matches =
            [
                .. operation.Matches.Select(match => new QueryMatchDefinition
                {
                    Query = new Dictionary<string, string>(match.Query, StringComparer.Ordinal),
                    Body = StubExampleSerializer.NormalizeValue(match.Body),
                    Response = new QueryMatchResponseDefinition
                    {
                        StatusCode = match.Response.StatusCode,
                        ResponseFile = StubDefinitionPathResolver.ResolveResponseFilePath(definitionDirectory, match.Response.ResponseFile),
                        Content = new Dictionary<string, MediaTypeDefinition>(match.Response.Content, StringComparer.Ordinal)
                    }
                })
            ],
            Responses = operation.Responses.ToDictionary(
                entry => entry.Key,
                entry => new ResponseDefinition
                {
                    Description = entry.Value.Description,
                    ResponseFile = StubDefinitionPathResolver.ResolveResponseFilePath(definitionDirectory, entry.Value.ResponseFile),
                    Content = new Dictionary<string, MediaTypeDefinition>(entry.Value.Content, StringComparer.Ordinal)
                },
                StringComparer.Ordinal)
        };
    }
}
