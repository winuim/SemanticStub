using Microsoft.AspNetCore.Http;

namespace SemanticStub.Api.Controllers;

internal static class StubRequestBodyReader
{
    private const string FormUrlEncodedMediaType = "application/x-www-form-urlencoded";

    internal static async Task<string?> ReadAsync(HttpRequest request)
    {
        try
        {
            request.EnableBuffering();

            if (IsFormUrlEncoded(request.ContentType))
            {
                var form = await request.ReadFormAsync();
                return SerializeFormBody(form);
            }

            using var reader = new StreamReader(request.Body, leaveOpen: true);
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }

            var body = await reader.ReadToEndAsync();

            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }

            return string.IsNullOrWhiteSpace(body) ? null : body;
        }
        catch (IOException)
        {
            return null;
        }
        catch (OperationCanceledException) when (request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return null;
        }
    }

    private static bool IsFormUrlEncoded(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var mediaType = contentType.Split(';', count: 2)[0].Trim();
        return string.Equals(mediaType, FormUrlEncodedMediaType, StringComparison.OrdinalIgnoreCase);
    }

    private static string? SerializeFormBody(IFormCollection form)
    {
        if (form.Count == 0)
        {
            return null;
        }

        var pairs = new List<string>();
        foreach (var field in form)
        {
            foreach (var value in field.Value)
            {
                pairs.Add($"{Uri.EscapeDataString(field.Key)}={Uri.EscapeDataString(value ?? string.Empty)}");
            }
        }

        return string.Join("&", pairs);
    }
}
