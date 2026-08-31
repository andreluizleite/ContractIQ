using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Knowledge;
using ContractIQ.Application.Knowledge.Indexing;
using ContractIQ.Infrastructure.Knowledge;
using ContractIQ.Infrastructure.Knowledge.AzureSearch;
using Xunit;

namespace ContractIQ.IntegrationTests;

public sealed class AzureAiSearchKnowledgeIndexTests
{
    private static readonly Guid CustomerId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ContractId =
        Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    [Fact]
    public void Creates_a_versioned_vector_index_schema()
    {
        SearchIndex index = AzureSearchGateway.CreateIndexDefinition(
            "contractiq-knowledge-v1",
            768);

        SearchField schemaVersion = Assert.Single(
            index.Fields,
            field => field.Name == nameof(AzureSearchKnowledgeChunkDocument.SchemaVersion));
        SearchField customerId = Assert.Single(
            index.Fields,
            field => field.Name == nameof(AzureSearchKnowledgeChunkDocument.CustomerId));
        SearchField content = Assert.Single(
            index.Fields,
            field => field.Name == nameof(AzureSearchKnowledgeChunkDocument.Content));
        SearchField embedding = Assert.Single(
            index.Fields,
            field => field.Name == nameof(AzureSearchKnowledgeChunkDocument.Embedding));

        Assert.True(schemaVersion.IsFilterable);
        Assert.True(customerId.IsFilterable);
        Assert.True(content.IsSearchable);
        Assert.Equal(768, embedding.VectorSearchDimensions);
        Assert.Equal(AzureSearchGateway.VectorProfileName, embedding.VectorSearchProfileName);
        Assert.Single(index.VectorSearch.Algorithms);
        Assert.Single(index.VectorSearch.Profiles);
    }

    [Fact]
    public void Builds_one_prefiltered_keyword_and_vector_query()
    {
        var query = new AzureSearchHybridQuery(
            "Can ACME cancel without a penalty?",
            Enumerable.Repeat(0.25f, 768).ToArray(),
            CustomerId,
            ContractId,
            new DateOnly(2026, 8, 28),
            50);

        SearchOptions options = AzureSearchGateway.CreateHybridSearchOptions(query);

        Assert.Equal(50, options.Size);
        Assert.Contains("CustomerId", options.Filter, StringComparison.Ordinal);
        Assert.Contains(CustomerId.ToString("D"), options.Filter, StringComparison.Ordinal);
        Assert.Contains("ContractId", options.Filter, StringComparison.Ordinal);
        Assert.Contains(ContractId.ToString("D"), options.Filter, StringComparison.Ordinal);
        Assert.Contains("EffectiveFrom", options.Filter, StringComparison.Ordinal);
        Assert.Equal(VectorFilterMode.PreFilter, options.VectorSearch.FilterMode);
        VectorizedQuery vectorQuery = Assert.IsType<VectorizedQuery>(
            Assert.Single(options.VectorSearch.Queries));
        Assert.Equal(50, vectorQuery.KNearestNeighborsCount);
        Assert.Contains(
            nameof(AzureSearchKnowledgeChunkDocument.Embedding),
            vectorQuery.Fields);
    }

    [Fact]
    public async Task Replacing_the_same_version_keeps_stable_chunk_keys_and_state()
    {
        var gateway = new InMemoryAzureSearchGateway();
        var index = new AzureAiSearchKnowledgeIndex(gateway);
        KnowledgeDocumentSource source = CreateSource();
        KnowledgeChunk[] chunks = CreateChunks();

        Assert.False(await index.IsCurrentAsync(
            source.DocumentKey,
            source.Version,
            "document-checksum",
            "embeddinggemma"));

        await index.ReplaceAsync(
            source,
            "document-checksum",
            "embeddinggemma",
            chunks);
        string[] firstKeys = gateway.Documents.Select(document => document.Id).ToArray();
        await index.ReplaceAsync(
            source,
            "document-checksum",
            "embeddinggemma",
            chunks);

        Assert.True(await index.IsCurrentAsync(
            source.DocumentKey,
            source.Version,
            "document-checksum",
            "embeddinggemma"));
        Assert.Equal(chunks.Length, gateway.Documents.Count);
        Assert.Equal(firstKeys, gateway.Documents.Select(document => document.Id));
    }

    [Fact]
    public async Task Document_indexer_skips_an_unchanged_azure_search_version()
    {
        KnowledgeDocumentSource source = CreateSource();
        var gateway = new InMemoryAzureSearchGateway();
        var handler = new IndexKnowledgeDocumentsHandler(
            new SingleDocumentCatalog(source),
            new DeterministicEmbeddings(),
            new AzureAiSearchKnowledgeIndex(gateway),
            new MarkdownKnowledgeChunker());

        IndexKnowledgeDocumentsResult first = await handler.HandleAsync();
        IndexKnowledgeDocumentsResult second = await handler.HandleAsync();

        Assert.Equal(1, first.IndexedDocuments);
        Assert.Equal(1, second.SkippedDocuments);
        Assert.Equal(1, gateway.ReplaceCount);
    }

    [Fact]
    public async Task Hybrid_results_map_to_application_evidence_and_preserve_citations()
    {
        var gateway = new InMemoryAzureSearchGateway();
        var index = new AzureAiSearchKnowledgeIndex(gateway);
        KnowledgeDocumentSource source = CreateSource();
        await index.ReplaceAsync(
            source,
            "document-checksum",
            "embeddinggemma",
            CreateChunks());
        gateway.SearchScore = 0.0325d;

        IReadOnlyList<KnowledgeEvidence> evidence = await index.SearchAsync(
            "Can ACME cancel without a penalty?",
            Enumerable.Repeat(0.25f, 768).ToArray(),
            CustomerId,
            ContractId,
            new DateOnly(2026, 8, 28),
            1);

        KnowledgeEvidence result = Assert.Single(evidence);
        Assert.Equal(source.DocumentKey, result.DocumentKey);
        Assert.Equal(source.SourcePath, result.SourcePath);
        Assert.Equal("Termination", result.Section);
        Assert.Equal(1, result.Page);
        Assert.Equal(0.0325d, result.Score);
        Assert.Null(result.LexicalScore);
        Assert.Null(result.VectorScore);
        Assert.NotEqual(Guid.Empty, result.ChunkId);
        Assert.NotNull(gateway.LastQuery);
        Assert.Equal(CustomerId, gateway.LastQuery.CustomerId);
        Assert.Equal(ContractId, gateway.LastQuery.ContractId);
        Assert.Equal(20, gateway.LastQuery.CandidateCount);
    }

    [Fact]
    public async Task Provider_failure_maps_to_the_safe_dependency_exception()
    {
        var gateway = new InMemoryAzureSearchGateway
        {
            SearchException = new RequestFailedException(503, "service unavailable"),
        };
        var index = new AzureAiSearchKnowledgeIndex(gateway);

        ExternalDependencyUnavailableException exception =
            await Assert.ThrowsAsync<ExternalDependencyUnavailableException>(
                () => index.SearchAsync(
                    "Can ACME cancel?",
                    Enumerable.Repeat(0.25f, 768).ToArray(),
                    CustomerId,
                    ContractId,
                    new DateOnly(2026, 8, 28),
                    5));

        Assert.Equal("azure-ai-search", exception.Dependency);
        Assert.DoesNotContain(
            "service unavailable",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static KnowledgeDocumentSource CreateSource() => new(
        "contract-acme",
        "ACME Agreement",
        KnowledgeDocumentType.Contract,
        "1.0",
        "en",
        CustomerId,
        ContractId,
        new DateOnly(2026, 1, 1),
        null,
        "contracts/acme.md",
        "## Termination\n\nThirty days notice is required.");

    private static KnowledgeChunk[] CreateChunks() =>
    [
        new KnowledgeChunk(
            0,
            "Termination",
            1,
            "Thirty days notice is required.",
            "chunk-checksum-1",
            Enumerable.Repeat(0.25f, 768).ToArray()),
        new KnowledgeChunk(
            1,
            "Penalty",
            2,
            "A deterministic penalty may apply.",
            "chunk-checksum-2",
            Enumerable.Repeat(0.5f, 768).ToArray()),
    ];

    private sealed class InMemoryAzureSearchGateway : IAzureSearchGateway
    {
        public List<AzureSearchKnowledgeChunkDocument> Documents { get; } = [];

        public AzureSearchHybridQuery? LastQuery { get; private set; }

        public double SearchScore { get; set; } = 1d;

        public Exception? SearchException { get; init; }

        public int ReplaceCount { get; private set; }

        public Task<bool> IsCurrentAsync(
            AzureSearchDocumentVersion version,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Documents.Any(document =>
                document.DocumentKey == version.DocumentKey &&
                document.Version == version.Version &&
                document.ContentChecksum == version.ContentChecksum &&
                document.EmbeddingModel == version.EmbeddingModel));

        public Task ReplaceAsync(
            string documentKey,
            string version,
            IReadOnlyList<AzureSearchKnowledgeChunkDocument> chunks,
            CancellationToken cancellationToken = default)
        {
            Documents.RemoveAll(document =>
                document.DocumentKey == documentKey && document.Version == version);
            Documents.AddRange(chunks);
            ReplaceCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AzureSearchHit>> HybridSearchAsync(
            AzureSearchHybridQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            if (SearchException is not null)
            {
                return Task.FromException<IReadOnlyList<AzureSearchHit>>(SearchException);
            }

            IReadOnlyList<AzureSearchHit> hits = Documents
                .Where(document =>
                    document.CustomerId is null ||
                    document.CustomerId == query.CustomerId.ToString("D"))
                .Where(document =>
                    document.ContractId is null ||
                    document.ContractId == query.ContractId.ToString("D"))
                .Take(query.CandidateCount)
                .Select(document => new AzureSearchHit(document, SearchScore))
                .ToArray();
            return Task.FromResult(hits);
        }
    }

    private sealed class SingleDocumentCatalog(KnowledgeDocumentSource source)
        : IKnowledgeDocumentCatalog
    {
        public Task<IReadOnlyList<KnowledgeDocumentSource>> ReadAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeDocumentSource>>([source]);
    }

    private sealed class DeterministicEmbeddings : IKnowledgeEmbeddingGenerator
    {
        public string ModelId => "embeddinggemma";

        public int Dimensions => 768;

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> values,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(
                values
                    .Select(_ => Enumerable.Repeat(0.25f, Dimensions).ToArray())
                    .ToArray());
    }
}
