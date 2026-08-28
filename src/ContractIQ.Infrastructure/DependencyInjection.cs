using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Application.Assistant;
using ContractIQ.Application.Knowledge;
using ContractIQ.Infrastructure.Assistant;
using ContractIQ.Infrastructure.Knowledge;
using ContractIQ.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

namespace ContractIQ.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "ContractIQ";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");

        KnowledgeOptions knowledgeOptions = KnowledgeOptions.FromConfiguration(configuration);
        AssistantOptions assistantOptions = AssistantOptions.FromConfiguration(configuration);
        return services.AddInfrastructure(connectionString, knowledgeOptions, assistantOptions);
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddInfrastructure(
            connectionString,
            new KnowledgeOptions(
                "sample-data/knowledge",
                new Uri("http://localhost:11434"),
                "embeddinggemma",
                768),
            new AssistantOptions(
                new Uri("http://localhost:11434"),
                "qwen3:4b",
                600,
                0.1f));
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        KnowledgeOptions knowledgeOptions)
    {
        return services.AddInfrastructure(
            connectionString,
            knowledgeOptions,
            new AssistantOptions(
                new Uri("http://localhost:11434"),
                "qwen3:4b",
                600,
                0.1f));
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        KnowledgeOptions knowledgeOptions,
        AssistantOptions assistantOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<ContractIqDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.SetPostgresVersion(18, 0);
                    npgsql.UseVector();
                }));

        services.AddHealthChecks()
            .AddDbContextCheck<ContractIqDbContext>(
                name: "postgresql",
                tags: ["ready"]);

        services.AddScoped<ICustomerRepository, PostgresCustomerRepository>();
        services.AddScoped<IContractRepository, PostgresContractRepository>();
        services.AddScoped<ICancellationRequestStore, PostgresCancellationRequestStore>();
        services.AddScoped<IKnowledgeIndex, PostgresKnowledgeIndex>();
        services.AddSingleton(knowledgeOptions);
        services.AddSingleton<IKnowledgeDocumentCatalog, FileSystemKnowledgeDocumentCatalog>();
        services.AddSingleton<IKnowledgeEmbeddingGenerator, OllamaKnowledgeEmbeddingGenerator>();
        services.AddSingleton(assistantOptions);
        services.AddSingleton<IAssistantAnswerGenerator, OllamaAssistantAnswerGenerator>();

        return services;
    }
}
