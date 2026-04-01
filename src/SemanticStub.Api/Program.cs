using Microsoft.AspNetCore.HttpLogging;
using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpLogging(options =>
{
    ConfigureHttpLoggingDefaults(options);
    builder.Configuration.GetSection("HttpLogging").Bind(options);
    ConfigureHttpLoggingMediaTypes(options);
});
builder.Services.Configure<StubSettings>(builder.Configuration.GetSection("StubSettings"));
builder.Services.AddSingleton<StubDefinitionLoader>();
builder.Services.AddSingleton<IStubDefinitionLoader>(serviceProvider => serviceProvider.GetRequiredService<StubDefinitionLoader>());
builder.Services.AddSingleton<StubDefinitionState>();
builder.Services.AddHostedService<StubDefinitionWatcher>();
builder.Services.AddSingleton<MatcherService>();
builder.Services.AddSingleton<ScenarioService>();
builder.Services.AddSingleton<IStubService>(serviceProvider => new StubService(
    serviceProvider.GetRequiredService<StubDefinitionState>(),
    serviceProvider.GetRequiredService<MatcherService>(),
    serviceProvider.GetRequiredService<ScenarioService>()));

var app = builder.Build();

// Fail fast during startup when stub definitions are invalid instead of deferring configuration errors until the first request.
app.Services.GetRequiredService<StubDefinitionState>();

app.UseHttpLogging();
app.MapControllers();

app.Run();

static void ConfigureHttpLoggingDefaults(HttpLoggingOptions options)
{
    options.LoggingFields =
        HttpLoggingFields.RequestPropertiesAndHeaders |
        HttpLoggingFields.ResponsePropertiesAndHeaders |
        HttpLoggingFields.RequestBody |
        HttpLoggingFields.ResponseBody;
}

static void ConfigureHttpLoggingMediaTypes(HttpLoggingOptions options)
{
    options.MediaTypeOptions.AddText("text/*");
    options.MediaTypeOptions.AddText("application/json");
    options.MediaTypeOptions.AddText("application/xml");
}

public partial class Program;
