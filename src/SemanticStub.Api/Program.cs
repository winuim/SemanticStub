using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<StubSettings>(builder.Configuration.GetSection("StubSettings"));
builder.Services.AddSingleton<StubDefinitionLoader>();
builder.Services.AddSingleton<StubService>();

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program;
