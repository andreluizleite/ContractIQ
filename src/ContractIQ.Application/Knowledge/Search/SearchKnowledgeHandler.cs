using ContractIQ.Application.Common.Exceptions;

namespace ContractIQ.Application.Knowledge.Search;

public sealed class SearchKnowledgeHandler(
    IKnowledgeEmbeddingGenerator embeddingGenerator,
    IKnowledgeIndex knowledgeIndex,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<KnowledgeEvidence>> HandleAsync(
        SearchKnowledgeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Query) || query.Query.Trim().Length < 3)
        {
            throw new ApplicationValidationException(
                nameof(query.Query),
                "Query must contain at least 3 characters.");
        }

        if (query.CustomerId == Guid.Empty)
        {
            throw new ApplicationValidationException(
                nameof(query.CustomerId),
                "Customer id is required.");
        }

        if (query.ContractId == Guid.Empty)
        {
            throw new ApplicationValidationException(
                nameof(query.ContractId),
                "Contract id is required.");
        }

        if (query.Limit is < 1 or > 20)
        {
            throw new ApplicationValidationException(
                nameof(query.Limit),
                "Limit must be between 1 and 20.");
        }

        IReadOnlyList<float[]> embeddings = await embeddingGenerator.GenerateAsync(
            [query.Query.Trim()],
            cancellationToken);

        if (embeddings.Count != 1 || embeddings[0].Length != embeddingGenerator.Dimensions)
        {
            throw new InvalidOperationException(
                $"Embedding model '{embeddingGenerator.ModelId}' returned an unexpected shape.");
        }

        DateOnly asOf = query.AsOf ?? DateOnly.FromDateTime(
            timeProvider.GetUtcNow().UtcDateTime);

        return await knowledgeIndex.SearchAsync(
            query.Query.Trim(),
            embeddings[0],
            query.CustomerId,
            query.ContractId,
            asOf,
            query.Limit,
            cancellationToken);
    }
}
