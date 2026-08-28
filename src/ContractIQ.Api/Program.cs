using System.Text.Json;
using System.Text.Json.Serialization;
using ContractIQ.Api.Endpoints;
using ContractIQ.Api.Errors;
using ContractIQ.Application.Cancellations.CreateCancellationRequest;
using ContractIQ.Application.Contracts.AssessCancellation;
using ContractIQ.Application.Contracts.GetContractDetails;
using ContractIQ.Application.Customers.ListCustomers;
using ContractIQ.Infrastructure;

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
builder.Services.AddScoped<AssessCancellationHandler>();
builder.Services.AddScoped<CreateCancellationRequestHandler>();
builder.Services.AddInfrastructure();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapApiV1();

app.Run();

// WebApplicationFactory uses this type as the entry point in integration tests.
public partial class Program;
