using ContractIQ.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractIQ.Infrastructure.Persistence;

public sealed class ContractIqDbContext(DbContextOptions<ContractIqDbContext> options)
    : DbContext(options)
{
    internal DbSet<CustomerRecord> Customers => Set<CustomerRecord>();

    internal DbSet<ContractRecord> Contracts => Set<ContractRecord>();

    internal DbSet<CancellationRequestRecord> CancellationRequests =>
        Set<CancellationRequestRecord>();

    internal DbSet<KnowledgeDocumentRecord> KnowledgeDocuments =>
        Set<KnowledgeDocumentRecord>();

    internal DbSet<KnowledgeChunkRecord> KnowledgeChunks =>
        Set<KnowledgeChunkRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContractIqDbContext).Assembly);
    }
}
