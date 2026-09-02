using ContractIQ.Application.Knowledge;
using ContractIQ.Application.Knowledge.Indexing;
using ContractIQ.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
builder.AddDocumentIndexerOpenTelemetry();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<MarkdownKnowledgeChunker>();
builder.Services.AddScoped<IndexKnowledgeDocumentsHandler>();
builder.Services.AddInfrastructure(builder.Configuration);

using IHost host = builder.Build();
await host.StartAsync();

try
{
    await host.Services.InitializeDatabaseAsync();

    await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
    IndexKnowledgeDocumentsResult result = await scope.ServiceProvider
        .GetRequiredService<IndexKnowledgeDocumentsHandler>()
        .HandleAsync();

    Console.WriteLine(
        $"Knowledge index ready. Indexed documents: {result.IndexedDocuments}; " +
        $"skipped unchanged documents: {result.SkippedDocuments}; " +
        $"new chunks: {result.IndexedChunks}.");
}
finally
{
    // Starting and stopping the host activates and flushes the optional
    // OpenTelemetry exporters before this short-lived process exits.
    await host.StopAsync();
}
