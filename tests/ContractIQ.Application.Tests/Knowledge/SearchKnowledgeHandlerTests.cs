using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Knowledge;
using ContractIQ.Application.Knowledge.Search;
using Xunit;

namespace ContractIQ.Application.Tests.Knowledge;

public sealed class SearchKnowledgeHandlerTests
{
    [Fact]
    public async Task HandleAsync_rejects_an_empty_customer_scope_before_embedding()
    {
        var embeddings = new Embeddings();
        var handler = new SearchKnowledgeHandler(
            embeddings,
            new Index(),
            new FrozenTimeProvider());

        ApplicationValidationException exception = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(new SearchKnowledgeQuery(
                "cancellation penalty",
                Guid.Empty,
                Guid.NewGuid())));

        Assert.Equal("CustomerId", exception.Field);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Fact]
    public async Task HandleAsync_rejects_an_oversized_query_before_embedding()
    {
        var embeddings = new Embeddings();
        var handler = new SearchKnowledgeHandler(
            embeddings,
            new Index(),
            new FrozenTimeProvider());

        ApplicationValidationException exception =
            await Assert.ThrowsAsync<ApplicationValidationException>(
                () => handler.HandleAsync(new SearchKnowledgeQuery(
                    new string('x', 1_001),
                    Guid.NewGuid(),
                    Guid.NewGuid())));

        Assert.Equal("Query", exception.Field);
        Assert.Equal(0, embeddings.CallCount);
    }

    private sealed class Embeddings : IKnowledgeEmbeddingGenerator
    {
        public int CallCount { get; private set; }

        public string ModelId => "test";

        public int Dimensions => 3;

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> values,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<float[]>>([new[] { 1f, 0f, 0f }]);
        }
    }

    private sealed class Index : IKnowledgeIndex
    {
        public Task<bool> IsCurrentAsync(string documentKey, string version, string contentChecksum, string embeddingModel, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ReplaceAsync(KnowledgeDocumentSource source, string contentChecksum, string embeddingModel, IReadOnlyList<KnowledgeChunk> chunks, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<KnowledgeEvidence>> SearchAsync(string query, float[] queryEmbedding, Guid customerId, Guid contractId, DateOnly asOf, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FrozenTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);
    }
}
