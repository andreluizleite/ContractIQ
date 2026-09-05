using ContractIQ.Application.Knowledge;
using ContractIQ.AzureSmokeTest;
using Xunit;

namespace ContractIQ.IntegrationTests;

public sealed class AzureSmokeTestRunnerTests
{
    [Fact]
    public async Task Runs_one_bounded_indexing_and_query_scenario()
    {
        var embeddings = new RecordingEmbeddingGenerator();
        var index = new RecordingKnowledgeIndex();
        var runner = new AzureSmokeTestRunner(
            embeddings,
            index,
            TimeProvider.System,
            "contractiq-smoke-v1");

        AzureSmokeTestReport report = await runner.RunAsync();

        Assert.Equal("succeeded", report.Outcome);
        Assert.Equal(1, report.EmbeddingRequests);
        Assert.Equal(2, report.EmbeddingInputs);
        Assert.Equal(1, report.IndexedDocuments);
        Assert.Equal(1, report.IndexedChunks);
        Assert.Equal(1, report.SearchQueries);
        Assert.Equal(1, report.SearchResultCount);
        Assert.Equal("contractiq-smoke-v1", report.IndexName);
        Assert.Equal(1, embeddings.RequestCount);
        Assert.Equal(2, embeddings.LastValues.Count);
        Assert.Equal(1, index.ReplaceCount);
        Assert.Equal(1, index.IsCurrentCount);
        Assert.Equal(1, index.SearchCount);
        Assert.Equal(1, index.LastChunkCount);
        Assert.Equal(1, index.LastLimit);
        Assert.Equal(AzureSmokeTestRunner.CustomerId, index.LastCustomerId);
        Assert.Equal(AzureSmokeTestRunner.ContractId, index.LastContractId);
    }

    [Fact]
    public async Task Fails_when_the_bounded_query_returns_no_evidence()
    {
        var index = new RecordingKnowledgeIndex { ReturnEvidence = false };
        var runner = new AzureSmokeTestRunner(
            new RecordingEmbeddingGenerator(),
            index,
            TimeProvider.System,
            "contractiq-smoke-v1");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync());

        Assert.Contains("no evidence", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, index.ReplaceCount);
        Assert.Equal(1, index.IsCurrentCount);
        Assert.Equal(1, index.SearchCount);
    }

    private sealed class RecordingEmbeddingGenerator : IKnowledgeEmbeddingGenerator
    {
        public string ModelId => "contractiq-embeddings";

        public int Dimensions => 768;

        public int RequestCount { get; private set; }

        public IReadOnlyList<string> LastValues { get; private set; } = [];

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> values,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            LastValues = values;
            return Task.FromResult<IReadOnlyList<float[]>>(
                values
                    .Select(_ => Enumerable.Repeat(0.25f, Dimensions).ToArray())
                    .ToArray());
        }
    }

    private sealed class RecordingKnowledgeIndex : IKnowledgeIndex
    {
        private KnowledgeDocumentSource? _source;

        public bool ReturnEvidence { get; init; } = true;

        public int ReplaceCount { get; private set; }

        public int IsCurrentCount { get; private set; }

        public int SearchCount { get; private set; }

        public int LastChunkCount { get; private set; }

        public int LastLimit { get; private set; }

        public Guid LastCustomerId { get; private set; }

        public Guid LastContractId { get; private set; }

        public Task<bool> IsCurrentAsync(
            string documentKey,
            string version,
            string contentChecksum,
            string embeddingModel,
            CancellationToken cancellationToken = default)
        {
            IsCurrentCount++;
            return Task.FromResult(_source is not null);
        }

        public Task ReplaceAsync(
            KnowledgeDocumentSource source,
            string contentChecksum,
            string embeddingModel,
            IReadOnlyList<KnowledgeChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            _source = source;
            ReplaceCount++;
            LastChunkCount = chunks.Count;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<KnowledgeEvidence>> SearchAsync(
            string query,
            float[] queryEmbedding,
            Guid customerId,
            Guid contractId,
            DateOnly asOf,
            int limit,
            CancellationToken cancellationToken = default)
        {
            SearchCount++;
            LastCustomerId = customerId;
            LastContractId = contractId;
            LastLimit = limit;

            if (!ReturnEvidence || _source is null)
            {
                return Task.FromResult<IReadOnlyList<KnowledgeEvidence>>([]);
            }

            IReadOnlyList<KnowledgeEvidence> evidence =
            [
                new KnowledgeEvidence(
                    Guid.Parse("99999999-9999-4999-8999-999999999999"),
                    _source.DocumentKey,
                    _source.Title,
                    _source.DocumentType,
                    _source.Version,
                    _source.Language,
                    _source.CustomerId,
                    _source.ContractId,
                    _source.EffectiveFrom,
                    _source.SourcePath,
                    "Cancellation notice",
                    1,
                    _source.Content,
                    1d,
                    null,
                    null),
            ];
            return Task.FromResult(evidence);
        }
    }
}
