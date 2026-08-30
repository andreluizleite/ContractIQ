using System.Diagnostics;
using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Common.Observability;

namespace ContractIQ.Application.Knowledge.Search;

public sealed class SearchKnowledgeHandler(
    IKnowledgeEmbeddingGenerator embeddingGenerator,
    IKnowledgeIndex knowledgeIndex,
    TimeProvider timeProvider) : IKnowledgeSearch
{
    private const int MaximumQueryCharacters = 1_000;

    public async Task<IReadOnlyList<KnowledgeEvidence>> HandleAsync(
        SearchKnowledgeQuery query,
        CancellationToken cancellationToken = default)
    {
        long startedAt = Stopwatch.GetTimestamp();
        using Activity? activity = ContractIqTelemetry.StartActivity(
            "contractiq.knowledge.search");

        try
        {
            ArgumentNullException.ThrowIfNull(query);

            if (string.IsNullOrWhiteSpace(query.Query) || query.Query.Trim().Length < 3)
            {
                throw new ApplicationValidationException(
                    nameof(query.Query),
                    "Query must contain at least 3 characters.");
            }

            if (query.Query.Length > MaximumQueryCharacters)
            {
                throw new ApplicationValidationException(
                    nameof(query.Query),
                    $"Query cannot exceed {MaximumQueryCharacters} characters.");
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

            activity?.SetTag("contractiq.knowledge.limit", query.Limit);

            IReadOnlyList<float[]> embeddings;
            using (Activity? embeddingActivity = ContractIqTelemetry.StartActivity(
                "contractiq.knowledge.embedding.generate"))
            {
                embeddingActivity?.SetTag(
                    "gen_ai.request.model",
                    embeddingGenerator.ModelId);

                try
                {
                    embeddings = await embeddingGenerator.GenerateAsync(
                        [query.Query.Trim()],
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    ContractIqTelemetry.MarkError(embeddingActivity, exception);
                    throw;
                }
            }

            if (embeddings.Count != 1 || embeddings[0].Length != embeddingGenerator.Dimensions)
            {
                throw new InvalidOperationException(
                    $"Embedding model '{embeddingGenerator.ModelId}' returned an unexpected shape.");
            }

            DateOnly asOf = query.AsOf ?? DateOnly.FromDateTime(
                timeProvider.GetUtcNow().UtcDateTime);

            IReadOnlyList<KnowledgeEvidence> evidence;
            using (Activity? indexActivity = ContractIqTelemetry.StartActivity(
                "contractiq.knowledge.index.query"))
            {
                try
                {
                    evidence = await knowledgeIndex.SearchAsync(
                        query.Query.Trim(),
                        embeddings[0],
                        query.CustomerId,
                        query.ContractId,
                        asOf,
                        query.Limit,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    ContractIqTelemetry.MarkError(indexActivity, exception);
                    throw;
                }

                indexActivity?.SetTag("contractiq.knowledge.result.count", evidence.Count);
            }

            activity?.SetTag("contractiq.knowledge.result.count", evidence.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            ContractIqTelemetry.RecordKnowledgeSearch(
                "succeeded",
                Stopwatch.GetElapsedTime(startedAt),
                evidence.Count);

            return evidence;
        }
        catch (Exception exception)
        {
            string outcome = exception is OperationCanceledException
                ? "cancelled"
                : "failed";

            ContractIqTelemetry.MarkError(activity, exception);
            ContractIqTelemetry.RecordKnowledgeSearch(
                outcome,
                Stopwatch.GetElapsedTime(startedAt),
                resultCount: 0);
            throw;
        }
    }
}
