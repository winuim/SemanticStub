using System.IO.Compression;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.RequestDecompression;
using Microsoft.AspNetCore.ResponseCompression;
using SemanticStub.Api.Extensions;
using SemanticStub.Application.Infrastructure.Yaml;
using SemanticStub.Infrastructure.Yaml;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpLogging(options =>
{
    ConfigureHttpLoggingDefaults(options);
    builder.Configuration.GetSection("HttpLogging").Bind(options);
    ConfigureHttpLoggingMediaTypes(options);
});
builder.Services.AddRequestDecompression();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.AddOptions<StubSettings>()
    .BindConfiguration("StubSettings")
    .Validate(
        s => !s.SemanticMatching.Enabled
             || (!string.IsNullOrWhiteSpace(s.SemanticMatching.Endpoint)
                 && Uri.TryCreate(s.SemanticMatching.Endpoint, UriKind.Absolute, out var uri)
                 && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)),
        "SemanticMatching.Endpoint must be a non-empty absolute HTTP or HTTPS URI when semantic matching is enabled.")
    .Validate(
        s => s.SemanticMatching.TimeoutSeconds > 0,
        "SemanticMatching.TimeoutSeconds must be positive.")
    .Validate(
        s => s.SemanticMatching.Threshold is >= -1.0 and <= 1.0,
        "SemanticMatching.Threshold must be within the cosine similarity range [-1.0, 1.0].")
    .Validate(
        s => s.SemanticMatching.TopScoreMargin >= 0,
        "SemanticMatching.TopScoreMargin must be non-negative.")
    .ValidateOnStart();
builder.Services.AddStubServices();

var app = builder.Build();

// Fail fast during startup when stub definitions are invalid instead of deferring configuration errors until the first request.
app.Services.GetRequiredService<StubDefinitionState>();

app.UseRequestDecompression();
app.UseResponseCompression();
app.UseHttpLogging();
app.MapControllers();

app.Run();

static void ConfigureHttpLoggingDefaults(HttpLoggingOptions options)
{
    options.LoggingFields =
        HttpLoggingFields.RequestPropertiesAndHeaders |
        HttpLoggingFields.ResponsePropertiesAndHeaders;
}

static void ConfigureHttpLoggingMediaTypes(HttpLoggingOptions options)
{
    options.MediaTypeOptions.AddText("text/*");
    options.MediaTypeOptions.AddText("application/json");
    options.MediaTypeOptions.AddText("application/xml");
}

public partial class Program;
