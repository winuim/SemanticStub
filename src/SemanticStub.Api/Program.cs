using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

app.MapControllers();

app.Run();

public partial class Program;
