using System.Diagnostics;
using ContractIQ.Application.Common.Observability;
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

    [Fact]
    public async Task HandleAsync_correlates_indexing_spans_without_document_content_or_identifiers()
    {
        KnowledgeDocumentSource source = CreateSource();
        var completedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = activitySource =>
                activitySource.Name is ContractIqTelemetry.ActivitySourceName or
                    "ContractIQ.Application.Tests",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = completedActivities.Add,
        };
        ActivitySource.AddActivityListener(listener);
        using var testSource = new ActivitySource("ContractIQ.Application.Tests");

        var handler = new IndexKnowledgeDocumentsHandler(
            new Catalog(source),
            new Embeddings(),
            new InMemoryKnowledgeIndex(),
            new MarkdownKnowledgeChunker());

        Activity root = testSource.StartActivity("test.indexing.request")!;
        ActivityTraceId expectedTraceId = root.TraceId;
        await handler.HandleAsync();
        root.Stop();

        Activity[] indexingActivities = completedActivities
            .Where(activity =>
                activity.Source.Name == ContractIqTelemetry.ActivitySourceName &&
                activity.TraceId == expectedTraceId)
            .ToArray();

        Activity indexing = Assert.Single(
            indexingActivities,
            activity => activity.OperationName == "contractiq.knowledge.index");
        Activity document = Assert.Single(
            indexingActivities,
            activity => activity.OperationName == "contractiq.knowledge.document.index");
        Activity check = Assert.Single(
            indexingActivities,
            activity => activity.OperationName == "contractiq.knowledge.index.check");
        Activity embeddings = Assert.Single(
            indexingActivities,
            activity => activity.OperationName == "contractiq.knowledge.embedding.generate");
        Activity replace = Assert.Single(
            indexingActivities,
            activity => activity.OperationName == "contractiq.knowledge.index.replace");

        Assert.Equal(root.SpanId, indexing.ParentSpanId);
        Assert.Equal(indexing.SpanId, document.ParentSpanId);
        Assert.Equal(document.SpanId, check.ParentSpanId);
        Assert.Equal(document.SpanId, embeddings.ParentSpanId);
        Assert.Equal(document.SpanId, replace.ParentSpanId);
        Assert.All(indexingActivities, activity =>
            Assert.Equal(ActivityStatusCode.Ok, activity.Status));

        string exportedTags = string.Join(
            '|',
            indexingActivities.SelectMany(activity => activity.TagObjects)
                .Select(tag => $"{tag.Key}={tag.Value}"));

        Assert.DoesNotContain(source.DocumentKey, exportedTags, StringComparison.Ordinal);
        Assert.DoesNotContain(source.Title, exportedTags, StringComparison.Ordinal);
        Assert.DoesNotContain(source.SourcePath, exportedTags, StringComparison.Ordinal);
        Assert.DoesNotContain(source.Content, exportedTags, StringComparison.Ordinal);
        Assert.DoesNotContain(source.CustomerId!.Value.ToString(), exportedTags, StringComparison.Ordinal);
        Assert.DoesNotContain(source.ContractId!.Value.ToString(), exportedTags, StringComparison.Ordinal);
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
