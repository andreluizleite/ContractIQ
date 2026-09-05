using Azure.Search.Documents.Indexes;

namespace ContractIQ.Infrastructure.Knowledge.AzureSearch;

internal sealed class AzureSearchKnowledgeChunkDocument
{
    [SimpleField(IsKey = true, IsFilterable = true)]
    public string Id { get; init; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    public int SchemaVersion { get; init; }

    [SimpleField(IsFilterable = true)]
    public string DocumentKey { get; init; } = string.Empty;

    [SearchableField]
    public string Title { get; init; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    public string DocumentType { get; init; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    public string Version { get; init; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    public string Language { get; init; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    public string? CustomerId { get; init; }

    [SimpleField(IsFilterable = true)]
    public string? ContractId { get; init; }

    [SimpleField(IsFilterable = true, IsSortable = true)]
    public DateTimeOffset EffectiveFrom { get; init; }

    [SimpleField(IsFilterable = true)]
    public DateTimeOffset? EffectiveTo { get; init; }

    [SimpleField]
    public string SourcePath { get; init; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    public string ContentChecksum { get; init; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    public string EmbeddingModel { get; init; } = string.Empty;

    [SimpleField(IsSortable = true)]
    public int ChunkIndex { get; init; }

    [SearchableField]
    public string Section { get; init; } = string.Empty;

    [SimpleField]
    public int Page { get; init; }

    [SearchableField]
    public string Content { get; init; } = string.Empty;

    [SimpleField]
    public string ChunkChecksum { get; init; } = string.Empty;

    [VectorSearchField(
        VectorSearchDimensions = KnowledgeOptions.StoredEmbeddingDimensions,
        VectorSearchProfileName = AzureSearchGateway.VectorProfileName,
        IsStored = false,
        IsHidden = true)]
    public float[] Embedding { get; init; } = [];
}
