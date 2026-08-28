using ContractIQ.Application.Knowledge;
using ContractIQ.Application.Knowledge.Indexing;
using Xunit;

namespace ContractIQ.Application.Tests.Knowledge;

public sealed class IndexKnowledgeDocumentsHandlerTests
{
    [Fact]
    public async Task HandleAsync_skips_an_unchanged_document_on_reindex()
    {
        KnowledgeDocumentSource source = CreateSource();
        var index = new InMemoryKnowledgeIndex();
        var handler = new IndexKnowledgeDocumentsHandler(
            new Catalog(source),
            new Embeddings(),
            index,
            new MarkdownKnowledgeChunker());

        IndexKnowledgeDocumentsResult first = await handler.HandleAsync();
        IndexKnowledgeDocumentsResult second = await handler.HandleAsync();

        Assert.Equal(1, first.IndexedDocuments);
        Assert.Equal(0, first.SkippedDocuments);
        Assert.True(first.IndexedChunks > 0);
        Assert.Equal(0, second.IndexedDocuments);
        Assert.Equal(1, second.SkippedDocuments);
        Assert.Equal(1, index.ReplaceCount);
    }

    private static KnowledgeDocumentSource CreateSource() => new(
        "contract-acme",
        "ACME Agreement",
        KnowledgeDocumentType.Contract,
        "1.0",
        "en",
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 1, 1),
        null,
        "contracts/acme.md",
        "## Termination\n\nThirty days notice is required.");

    private sealed class Catalog(KnowledgeDocumentSource source) : IKnowledgeDocumentCatalog
    {
        public Task<IReadOnlyList<KnowledgeDocumentSource>> ReadAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeDocumentSource>>([source]);
    }

    private sealed class Embeddings : IKnowledgeEmbeddingGenerator
    {
        public string ModelId => "test-embedding";

        public int Dimensions => 3;

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> values,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(
                values.Select(_ => new[] { 1f, 0f, 0f }).ToArray());
    }

    private sealed class InMemoryKnowledgeIndex : IKnowledgeIndex
    {
        private readonly HashSet<string> _checksums = [];

        public int ReplaceCount { get; private set; }

        public Task<bool> IsCurrentAsync(
            string documentKey,
            string version,
            string contentChecksum,
            string embeddingModel,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_checksums.Contains(contentChecksum));

        public Task ReplaceAsync(
            KnowledgeDocumentSource source,
            string contentChecksum,
            string embeddingModel,
            IReadOnlyList<KnowledgeChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            _checksums.Add(contentChecksum);
            ReplaceCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<KnowledgeEvidence>> SearchAsync(
            string query,
            float[] queryEmbedding,
            Guid customerId,
            Guid contractId,
            DateOnly asOf,
            int limit,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
