namespace SemanticStub.Api.Infrastructure.Yaml;

internal static class StubDefinitionPathResolver
{
    public static string? ResolveResponseFilePath(string definitionDirectory, string? responseFile)
    {
        if (string.IsNullOrWhiteSpace(responseFile))
        {
            return responseFile;
        }

        if (Path.IsPathRooted(responseFile))
        {
            return responseFile;
        }

        return Path.GetFullPath(Path.Combine(definitionDirectory, responseFile));
    }
}
