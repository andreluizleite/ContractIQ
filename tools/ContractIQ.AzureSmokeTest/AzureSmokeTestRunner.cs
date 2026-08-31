using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ContractIQ.Application.Knowledge;

namespace ContractIQ.AzureSmokeTest;

internal sealed class AzureSmokeTestRunner(
    IKnowledgeEmbeddingGenerator embeddingGenerator,
    IKnowledgeIndex knowledgeIndex,
    TimeProvider timeProvider,
    string indexName)
{
    internal static readonly Guid CustomerId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");
    internal static readonly Guid ContractId =
        Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    private const string DocumentKey = "contractiq-azure-smoke-policy";
    private const string DocumentVersion = "1";
    private const string Content =
        "A cancellation request requires 30 days written notice before termination.";
    private const string Query =
        "How much written notice is required for cancellation?";

    public async Task<AzureSmokeTestReport> RunAsync(
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        long timestamp = Stopwatch.GetTimestamp();
        string[] embeddingInputs = [Content, Query];

        IReadOnlyList<float[]> embeddings = await embeddingGenerator.GenerateAsync(
            embeddingInputs,
            cancellationToken);
        ValidateEmbeddings(embeddings);

        var source = new KnowledgeDocumentSource(
            DocumentKey,
            "ContractIQ Azure smoke-test policy",
            KnowledgeDocumentType.Policy,
            DocumentVersion,
            "en",
            CustomerId,
            ContractId,
            new DateOnly(2026, 1, 1),
            null,
            "smoke/contractiq-azure-policy.md",
            Content);
        var chunk = new KnowledgeChunk(
            0,
            "Cancellation notice",
            1,
            Content,
            ComputeChecksum(Content),
            embeddings[0]);

        await knowledgeIndex.ReplaceAsync(
            source,
            ComputeChecksum(source.Content),
            embeddingGenerator.ModelId,
            [chunk],
            cancellationToken);

        IReadOnlyList<KnowledgeEvidence> evidence = await knowledgeIndex.SearchAsync(
            Query,
            embeddings[1],
            CustomerId,
            ContractId,
            new DateOnly(2026, 8, 31),
            limit: 1,
            cancellationToken);
        KnowledgeEvidence result = evidence.SingleOrDefault()
            ?? throw new InvalidOperationException(
                "The bounded hybrid query returned no evidence.");

        if (!string.Equals(result.DocumentKey, DocumentKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The bounded hybrid query returned unexpected evidence.");
        }

        return new AzureSmokeTestReport(
            "succeeded",
            startedAt,
            Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds,
            "foundry",
            embeddingGenerator.ModelId,
            EmbeddingRequests: 1,
            EmbeddingInputs: embeddingInputs.Length,
            EmbeddingInputCharacters: embeddingInputs.Sum(value => value.Length),
            EmbeddingDimensions: embeddingGenerator.Dimensions,
            IndexedDocuments: 1,
            IndexedChunks: 1,
            SearchQueries: 1,
            SearchResultCount: evidence.Count,
            SearchProvider: "azure-ai-search",
            IndexName: indexName);
    }

    private void ValidateEmbeddings(IReadOnlyList<float[]> embeddings)
    {
        if (embeddings.Count != 2 ||
            embeddings.Any(value => value.Length != embeddingGenerator.Dimensions))
        {
            throw new InvalidOperationException(
                "Foundry returned an unexpected embedding result shape.");
        }
    }

    private static string ComputeChecksum(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
