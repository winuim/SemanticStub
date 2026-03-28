using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<StubSettings>(builder.Configuration.GetSection("StubSettings"));
builder.Services.AddSingleton<IStubDefinitionLoader, StubDefinitionLoader>();
builder.Services.AddSingleton<MatcherService>();
builder.Services.AddSingleton<ScenarioService>();
builder.Services.AddSingleton<IStubService, StubService>();

var app = builder.Build();

// Fail fast during startup when stub definitions are invalid instead of deferring configuration errors until the first request.
app.Services.GetRequiredService<IStubService>();

app.MapControllers();

app.Run();

public partial class Program;
