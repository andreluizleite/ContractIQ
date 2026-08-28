using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Infrastructure;
using ContractIQ.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using Respawn;
using Respawn.Graph;

namespace ContractIQ.IntegrationTests;

internal sealed class ContractIqApiFactory(
    string connectionString,
    DateTimeOffset utcNow,
    TimeZoneInfo? localTimeZone = null) : WebApplicationFactory<Program>
{
    private Respawner? _respawner;

    public async Task ResetAndSeedDatabaseAsync(CancellationToken cancellationToken = default)
    {
        using (var migrationScope = Services.CreateScope())
        {
            var dbContext = migrationScope.ServiceProvider
                .GetRequiredService<ContractIqDbContext>();

            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        _respawner ??= await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = [new Table("public", "__EFMigrationsHistory")],
            });

        await _respawner.ResetAsync(connection);

        using var seedScope = Services.CreateScope();
        var seedDbContext = seedScope.ServiceProvider
            .GetRequiredService<ContractIqDbContext>();

        await DemoDataSeeder.SeedAsync(seedDbContext, cancellationToken);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ContractIqDbContext>();
            services.RemoveAll<DbContextOptions<ContractIqDbContext>>();
            services.RemoveAll<ICustomerRepository>();
            services.RemoveAll<IContractRepository>();
            services.RemoveAll<ICancellationRequestStore>();
            services.RemoveAll<IConfigureOptions<HealthCheckServiceOptions>>();
            services.AddInfrastructure(connectionString);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(
                new FrozenTimeProvider(utcNow, localTimeZone ?? TimeZoneInfo.Utc));
        });
    }

    private sealed class FrozenTimeProvider(
        DateTimeOffset utcNow,
        TimeZoneInfo localTimeZone) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => localTimeZone;

        public override DateTimeOffset GetUtcNow() => utcNow.ToUniversalTime();
    }
}
