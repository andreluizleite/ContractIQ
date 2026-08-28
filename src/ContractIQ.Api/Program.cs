var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

// WebApplicationFactory uses this type as the entry point in integration tests.
public partial class Program;
