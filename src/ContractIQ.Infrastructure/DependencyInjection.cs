using ContractIQ.Application.Abstractions.Persistence;
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

        return services.AddInfrastructure(connectionString);
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
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

        return services;
    }
}
