using ContractIQ.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContractIQ.Infrastructure.Persistence.Configurations;

internal sealed class KnowledgeDocumentRecordConfiguration
    : IEntityTypeConfiguration<KnowledgeDocumentRecord>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocumentRecord> builder)
    {
        builder.ToTable("knowledge_documents");
        builder.HasKey(document => document.Id).HasName("pk_knowledge_documents");

        builder.Property(document => document.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(document => document.DocumentKey).HasColumnName("document_key").HasMaxLength(200);
        builder.Property(document => document.Title).HasColumnName("title").HasMaxLength(300);
        builder.Property(document => document.DocumentType).HasColumnName("document_type").HasConversion<string>().HasMaxLength(30);
        builder.Property(document => document.Version).HasColumnName("version").HasMaxLength(50);
        builder.Property(document => document.Language).HasColumnName("language").HasMaxLength(10);
        builder.Property(document => document.CustomerId).HasColumnName("customer_id");
        builder.Property(document => document.ContractId).HasColumnName("contract_id");
        builder.Property(document => document.EffectiveFrom).HasColumnName("effective_from");
        builder.Property(document => document.EffectiveTo).HasColumnName("effective_to");
        builder.Property(document => document.SourcePath).HasColumnName("source_path").HasMaxLength(500);
        builder.Property(document => document.ContentChecksum).HasColumnName("content_checksum").HasMaxLength(64);
        builder.Property(document => document.EmbeddingModel).HasColumnName("embedding_model").HasMaxLength(200);
        builder.Property(document => document.IndexedAtUtc).HasColumnName("indexed_at_utc");

        builder.HasIndex(document => new { document.DocumentKey, document.Version })
            .HasDatabaseName("ux_knowledge_documents_key_version")
            .IsUnique();
        builder.HasIndex(document => new
        {
            document.CustomerId,
            document.ContractId,
            document.EffectiveFrom,
        }).HasDatabaseName("ix_knowledge_documents_scope_effective_from");
    }
}
