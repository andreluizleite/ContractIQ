using System.Text.Json;
using ContractIQ.Application.Knowledge;
using ContractIQ.Infrastructure;
using ContractIQ.Infrastructure.Knowledge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ContractIQ.AzureSmokeTest;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await RunAsync(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Azure AI smoke test failed ({exception.GetType().Name}). " +
                "Review the Azure roles, endpoints, deployments, and workflow variables.");
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped(serviceProvider =>
        {
            KnowledgeIndexOptions options = serviceProvider
                .GetRequiredService<KnowledgeIndexOptions>();
            return new AzureSmokeTestRunner(
                serviceProvider.GetRequiredService<IKnowledgeEmbeddingGenerator>(),
                serviceProvider.GetRequiredService<IKnowledgeIndex>(),
                serviceProvider.GetRequiredService<TimeProvider>(),
                options.AzureSearchIndexName);
        });

        using IHost host = builder.Build();
        int timeoutSeconds = ReadTimeoutSeconds(
            builder.Configuration["AzureSmoke:TimeoutSeconds"]);
        string reportPath = builder.Configuration["AzureSmoke:ReportPath"]
            ?? "TestResults/azure-smoke-test.json";
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(timeoutSeconds));

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        AzureSmokeTestReport report = await scope.ServiceProvider
            .GetRequiredService<AzureSmokeTestRunner>()
            .RunAsync(timeout.Token);
        string json = JsonSerializer.Serialize(
            report,
            new JsonSerializerOptions { WriteIndented = true });

        string? directory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(reportPath, json, timeout.Token);
        Console.WriteLine(json);
        return 0;
    }

    private static int ReadTimeoutSeconds(string? value)
    {
        int timeoutSeconds = int.TryParse(value, out int configuredTimeout)
            ? configuredTimeout
            : 90;

        if (timeoutSeconds is < 30 or > 180)
        {
            throw new InvalidOperationException(
                "Azure smoke-test timeout must be between 30 and 180 seconds.");
        }

        return timeoutSeconds;
    }
}
