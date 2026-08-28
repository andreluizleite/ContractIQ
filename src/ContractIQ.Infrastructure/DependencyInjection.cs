using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Application.Knowledge;
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
        return services.AddInfrastructure(connectionString, knowledgeOptions);
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
                768));
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        KnowledgeOptions knowledgeOptions)
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

        return services;
    }
}
