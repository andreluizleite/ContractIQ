using System.Text.Json;
using System.Text.Json.Serialization;
using ContractIQ.Api.Endpoints;
using ContractIQ.Api.Errors;
using ContractIQ.Api.Health;
using ContractIQ.Application.Assistant;
using ContractIQ.Application.Cancellations.CreateCancellationRequest;
using ContractIQ.Application.Contracts.AssessCancellation;
using ContractIQ.Application.Contracts.GetContractDetails;
using ContractIQ.Application.Contracts.ListCustomerContracts;
using ContractIQ.Application.Customers.ListCustomers;
using ContractIQ.Application.Knowledge;
using ContractIQ.Application.Knowledge.Search;
using ContractIQ.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ListCustomersHandler>();
builder.Services.AddScoped<GetContractDetailsHandler>();
builder.Services.AddScoped<ListCustomerContractsHandler>();
builder.Services.AddScoped<AssessCancellationHandler>();
builder.Services.AddScoped<CreateCancellationRequestHandler>();
builder.Services.AddSingleton<MarkdownKnowledgeChunker>();
builder.Services.AddScoped<IKnowledgeSearch, SearchKnowledgeHandler>();
builder.Services.AddSingleton<GroundedAnswerPromptBuilder>();
builder.Services.AddScoped<AskContractQuestionHandler>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var readinessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync,
};

app.MapHealthChecks("/health", readinessOptions);
app.MapHealthChecks("/health/ready", readinessOptions);
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync,
});
app.MapApiV1();

app.Run();

// WebApplicationFactory uses this type as the entry point in integration tests.
public partial class Program;
