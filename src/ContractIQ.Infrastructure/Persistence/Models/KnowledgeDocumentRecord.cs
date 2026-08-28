using ContractIQ.Application.Knowledge;

namespace ContractIQ.Infrastructure.Persistence.Models;

internal sealed class KnowledgeDocumentRecord
{
    public Guid Id { get; set; }

    public required string DocumentKey { get; set; }

    public required string Title { get; set; }

    public KnowledgeDocumentType DocumentType { get; set; }

    public required string Version { get; set; }

    public required string Language { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? ContractId { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public required string SourcePath { get; set; }

    public required string ContentChecksum { get; set; }

    public required string EmbeddingModel { get; set; }

    public DateTimeOffset IndexedAtUtc { get; set; }

    public List<KnowledgeChunkRecord> Chunks { get; set; } = [];
}
