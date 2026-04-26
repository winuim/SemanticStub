using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using SemanticStub.Api.Models;
using SemanticStub.Application.Models;
using SemanticStub.Application.Utilities;

namespace SemanticStub.Api.Services;

internal sealed record TemplateSubstitutionContext(
    IReadOnlyDictionary<string, string> PathParameters,
    IReadOnlyDictionary<string, StringValues> Query,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);

internal sealed class StubResponseBuilder
{
    private const string JsonContentType = "application/json";
    private readonly Func<string, string> _responseFileReader;

    public StubResponseBuilder(Func<string, string> responseFileReader)
    {
        _responseFileReader = responseFileReader;
    }

    public bool TryBuild(int statusCode, ResponseDefinition responseDefinition, out StubResponse response, TemplateSubstitutionContext? context = null)
    {
        response = null!;

        if (!string.IsNullOrEmpty(responseDefinition.ResponseFile))
        {
            response = CreateStubResponse(
                statusCode,
                responseDefinition.DelayMilliseconds,
                responseDefinition.Content,
                responseDefinition.Headers,
                responseDefinition.ResponseFile);

            return true;
        }

        var responseBody = BuildResponseBody(responseDefinition.Content);

        if (responseBody is null)
        {
            return false;
        }

        if (context is not null)
        {
            responseBody = ApplySubstitution(responseBody, context);
        }

        response = CreateStubResponse(
            statusCode,
            responseDefinition.DelayMilliseconds,
            responseDefinition.Content,
            responseDefinition.Headers,
            responseBody,
            filePath: null);

        return true;
    }

    public bool TryBuild(QueryMatchResponseDefinition responseDefinition, out StubResponse response, TemplateSubstitutionContext? context = null)
    {
        response = null!;

        if (responseDefinition.StatusCode <= 0)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(responseDefinition.ResponseFile))
        {
            response = CreateStubResponse(
                responseDefinition.StatusCode,
                responseDefinition.DelayMilliseconds,
                responseDefinition.Content,
                responseDefinition.Headers,
                responseDefinition.ResponseFile);

            return true;
        }

        var responseBody = BuildResponseBody(responseDefinition.Content);

        if (responseBody is null)
        {
            return false;
        }

        if (context is not null)
        {
            responseBody = ApplySubstitution(responseBody, context);
        }

        response = CreateStubResponse(
            responseDefinition.StatusCode,
            responseDefinition.DelayMilliseconds,
            responseDefinition.Content,
            responseDefinition.Headers,
            responseBody,
            filePath: null);

        return true;
    }

    private static StubResponse CreateStubResponse(
        int statusCode,
        int? delayMilliseconds,
        IReadOnlyDictionary<string, MediaTypeDefinition> content,
        IReadOnlyDictionary<string, HeaderDefinition> headers,
        string responseBody,
        string? filePath)
    {
        return new StubResponse
        {
            StatusCode = statusCode,
            DelayMilliseconds = delayMilliseconds,
            ContentType = ResolveContentType(content),
            Headers = StubResponseHeaderBuilder.BuildResponseHeaders(headers),
            Body = responseBody,
            FilePath = filePath
        };
    }

    private StubResponse CreateStubResponse(
        int statusCode,
        int? delayMilliseconds,
        IReadOnlyDictionary<string, MediaTypeDefinition> content,
        IReadOnlyDictionary<string, HeaderDefinition> headers,
        string responseFile)
    {
        if (Path.IsPathRooted(responseFile))
        {
            return CreateStubResponse(
                statusCode,
                delayMilliseconds,
                content,
                headers,
                string.Empty,
                responseFile);
        }

        return CreateStubResponse(
            statusCode,
            delayMilliseconds,
            content,
            headers,
            _responseFileReader(responseFile),
            filePath: null);
    }

    private static string? BuildResponseBody(IReadOnlyDictionary<string, MediaTypeDefinition> content)
    {
        var selectedKey = SelectMediaTypeKey(content);

        if (selectedKey is null || !content.TryGetValue(selectedKey, out var mediaType) || mediaType.Example is null)
        {
            return null;
        }

        if (!IsJsonContentType(selectedKey) && mediaType.Example is string rawExample)
        {
            return rawExample;
        }

        return StubExampleSerializer.Serialize(mediaType.Example);
    }

    private static string ResolveContentType(IReadOnlyDictionary<string, MediaTypeDefinition> content)
    {
        return SelectMediaTypeKey(content) ?? JsonContentType;
    }

    private static string? SelectMediaTypeKey(IReadOnlyDictionary<string, MediaTypeDefinition> content)
    {
        return content.Keys.FirstOrDefault(IsJsonContentType) ?? content.Keys.FirstOrDefault();
    }

    private static bool IsJsonContentType(string contentType)
    {
        return contentType.Equals(JsonContentType, StringComparison.OrdinalIgnoreCase)
            || contentType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly Regex TemplatePlaceholder = new(@"\{\{(\w+)\.([-\w]+)\}\}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static string ApplySubstitution(string body, TemplateSubstitutionContext context)
    {
        return TemplatePlaceholder.Replace(body, match =>
        {
            var source = match.Groups[1].Value;
            var name = match.Groups[2].Value;

            return source switch
            {
                "path" => context.PathParameters.TryGetValue(name, out var pathVal) ? pathVal : match.Value,
                "query" => context.Query.TryGetValue(name, out var queryVal) ? queryVal.FirstOrDefault() ?? match.Value : match.Value,
                "header" => context.Headers.TryGetValue(name, out var headerVal) ? headerVal : match.Value,
                "body" => TryExtractBodyValue(context.Body, name, out var bodyVal) ? bodyVal : match.Value,
                _ => match.Value,
            };
        });
    }

    private static bool TryExtractBodyValue(string? body, string key, out string value)
    {
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty(key, out var element))
            {
                return false;
            }

            value = element.ValueKind == JsonValueKind.String
                ? element.GetString() ?? string.Empty
                : element.GetRawText();

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
