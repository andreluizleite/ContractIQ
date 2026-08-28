using NpgsqlTypes;
using Pgvector;

namespace ContractIQ.Infrastructure.Persistence.Models;

internal sealed class KnowledgeChunkRecord
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public required string Section { get; set; }

    public int Page { get; set; }

    public required string Content { get; set; }

    public required string ContentChecksum { get; set; }

    public required Vector Embedding { get; set; }

    public NpgsqlTsVector SearchVector { get; set; } = null!;

    public KnowledgeDocumentRecord Document { get; set; } = null!;
}
