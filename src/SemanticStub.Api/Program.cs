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
builder.Services.Configure<StubSettings>(builder.Configuration.GetSection("StubSettings"));
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
