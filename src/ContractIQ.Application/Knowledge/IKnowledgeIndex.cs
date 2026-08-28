namespace ContractIQ.Application.Knowledge;

public interface IKnowledgeIndex
{
    Task<bool> IsCurrentAsync(
        string documentKey,
        string version,
        string contentChecksum,
        string embeddingModel,
        CancellationToken cancellationToken = default);

    Task ReplaceAsync(
        KnowledgeDocumentSource source,
        string contentChecksum,
        string embeddingModel,
        IReadOnlyList<KnowledgeChunk> chunks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeEvidence>> SearchAsync(
        string query,
        float[] queryEmbedding,
        Guid customerId,
        Guid contractId,
        DateOnly asOf,
        int limit,
        CancellationToken cancellationToken = default);
}
