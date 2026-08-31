using Azure.Core;
using Azure.Identity;
using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Application.Assistant;
using ContractIQ.Application.Assistant.Tools;
using ContractIQ.Application.Knowledge;
using ContractIQ.Infrastructure.AI;
using ContractIQ.Infrastructure.Assistant;
using ContractIQ.Infrastructure.Knowledge;
using ContractIQ.Infrastructure.Knowledge.AzureSearch;
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
        KnowledgeIndexOptions knowledgeIndexOptions =
            KnowledgeIndexOptions.FromConfiguration(configuration);
        AssistantOptions assistantOptions = AssistantOptions.FromConfiguration(configuration);
        return services.AddInfrastructure(
            connectionString,
            knowledgeOptions,
            assistantOptions,
            knowledgeIndexOptions);
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddInfrastructure(
            connectionString,
            new KnowledgeOptions(
                "sample-data/knowledge",
                KnowledgeEmbeddingProvider.Ollama,
                new Uri("http://localhost:11434"),
                "embeddinggemma",
                768),
            new AssistantOptions(
                AssistantProvider.Ollama,
                new Uri("http://localhost:11434"),
                "qwen3:4b",
                null,
                600,
                0.1f),
            KnowledgeIndexOptions.Local);
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
                AssistantProvider.Ollama,
                new Uri("http://localhost:11434"),
                "qwen3:4b",
                null,
                600,
                0.1f),
            KnowledgeIndexOptions.Local);
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        KnowledgeOptions knowledgeOptions,
        AssistantOptions assistantOptions,
        KnowledgeIndexOptions? knowledgeIndexOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        knowledgeIndexOptions ??= KnowledgeIndexOptions.Local;

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
        services.AddSingleton(knowledgeOptions);
        services.AddSingleton(knowledgeIndexOptions);
        services.AddSingleton<IKnowledgeDocumentCatalog, FileSystemKnowledgeDocumentCatalog>();
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        if (knowledgeIndexOptions.Provider == KnowledgeIndexProvider.AzureAiSearch)
        {
            services.AddSingleton<IAzureSearchGateway, AzureSearchGateway>();
            services.AddScoped<IKnowledgeIndex, AzureAiSearchKnowledgeIndex>();
        }
        else
        {
            services.AddScoped<IKnowledgeIndex, PostgresKnowledgeIndex>();
        }
        services.AddSingleton<FoundryOpenAIClientFactory>();
        if (knowledgeOptions.EmbeddingProvider == KnowledgeEmbeddingProvider.Foundry)
        {
            services.AddSingleton<IKnowledgeEmbeddingGenerator, FoundryKnowledgeEmbeddingGenerator>();
        }
        else
        {
            services.AddSingleton<IKnowledgeEmbeddingGenerator, OllamaKnowledgeEmbeddingGenerator>();
        }
        services.AddSingleton(assistantOptions);
        services.AddSingleton<IAssistantToolAudit, LoggingAssistantToolAudit>();
        services.AddScoped<IAssistantWriteTransaction, EfAssistantWriteTransaction>();
        services.AddScoped<IAssistantAnswerGenerator, ChatClientAssistantAnswerGenerator>();

        return services;
    }
}
