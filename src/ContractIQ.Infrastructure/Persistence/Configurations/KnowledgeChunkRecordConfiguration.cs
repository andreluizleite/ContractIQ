using ContractIQ.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContractIQ.Infrastructure.Persistence.Configurations;

internal sealed class KnowledgeChunkRecordConfiguration
    : IEntityTypeConfiguration<KnowledgeChunkRecord>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunkRecord> builder)
    {
        builder.ToTable("knowledge_chunks");
        builder.HasKey(chunk => chunk.Id).HasName("pk_knowledge_chunks");

        builder.Property(chunk => chunk.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(chunk => chunk.DocumentId).HasColumnName("document_id");
        builder.Property(chunk => chunk.ChunkIndex).HasColumnName("chunk_index");
        builder.Property(chunk => chunk.Section).HasColumnName("section").HasMaxLength(300);
        builder.Property(chunk => chunk.Page).HasColumnName("page");
        builder.Property(chunk => chunk.Content).HasColumnName("content");
        builder.Property(chunk => chunk.ContentChecksum).HasColumnName("content_checksum").HasMaxLength(64);
        builder.Property(chunk => chunk.Embedding).HasColumnName("embedding").HasColumnType("vector(768)");
        builder.Property(chunk => chunk.SearchVector)
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "to_tsvector('simple', coalesce(section, '') || ' ' || coalesce(content, ''))",
                stored: true);

        builder.HasIndex(chunk => new { chunk.DocumentId, chunk.ChunkIndex })
            .HasDatabaseName("ux_knowledge_chunks_document_index")
            .IsUnique();

        builder.HasOne(chunk => chunk.Document)
            .WithMany(document => document.Chunks)
            .HasForeignKey(chunk => chunk.DocumentId)
            .HasConstraintName("fk_knowledge_chunks_documents_document_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
