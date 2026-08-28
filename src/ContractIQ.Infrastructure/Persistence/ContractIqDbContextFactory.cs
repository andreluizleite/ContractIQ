using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace ContractIQ.Infrastructure.Persistence;

internal sealed class ContractIqDbContextFactory :
    IDesignTimeDbContextFactory<ContractIqDbContext>
{
    private const string LocalDemoConnectionString =
        "Host=localhost;Port=5432;Database=contractiq;Username=contractiq;Password=contractiq";

    public ContractIqDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__ContractIQ")
            ?? LocalDemoConnectionString;

        var options = new DbContextOptionsBuilder<ContractIqDbContext>();
        options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.SetPostgresVersion(18, 0);
                npgsql.UseVector();
            });

        return new ContractIqDbContext(options.Options);
    }
}
