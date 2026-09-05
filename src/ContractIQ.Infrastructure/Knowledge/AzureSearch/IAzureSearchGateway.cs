namespace ContractIQ.Infrastructure.Knowledge.AzureSearch;

internal interface IAzureSearchGateway
{
    Task<bool> IsCurrentAsync(
        AzureSearchDocumentVersion version,
        CancellationToken cancellationToken = default);

    Task ReplaceAsync(
        string documentKey,
        string version,
        IReadOnlyList<AzureSearchKnowledgeChunkDocument> chunks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AzureSearchHit>> HybridSearchAsync(
        AzureSearchHybridQuery query,
        CancellationToken cancellationToken = default);
}

internal sealed record AzureSearchDocumentVersion(
    string DocumentKey,
    string Version,
    string ContentChecksum,
    string EmbeddingModel);

internal sealed record AzureSearchHybridQuery(
    string Text,
    float[] Vector,
    Guid CustomerId,
    Guid ContractId,
    DateOnly AsOf,
    int CandidateCount);

internal sealed record AzureSearchHit(
    AzureSearchKnowledgeChunkDocument Document,
    double Score);
