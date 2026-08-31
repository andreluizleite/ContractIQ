using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Identity;
using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Common.Observability;
using ContractIQ.Application.Knowledge;

namespace ContractIQ.Infrastructure.Knowledge.AzureSearch;

internal sealed class AzureAiSearchKnowledgeIndex(
    IAzureSearchGateway gateway) : IKnowledgeIndex
{
    private const string ProviderName = "azure_ai_search";

    public Task<bool> IsCurrentAsync(
        string documentKey,
        string version,
        string contentChecksum,
        string embeddingModel,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "is_current",
            () => gateway.IsCurrentAsync(
                new AzureSearchDocumentVersion(
                    documentKey,
                    version,
                    contentChecksum,
                    embeddingModel),
                cancellationToken),
            isCurrent => isCurrent ? 1 : 0,
            cancellationToken);

    public Task ReplaceAsync(
        KnowledgeDocumentSource source,
        string contentChecksum,
        string embeddingModel,
        IReadOnlyList<KnowledgeChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        AzureSearchKnowledgeChunkDocument[] documents = chunks
            .Select(chunk => MapDocument(source, contentChecksum, embeddingModel, chunk))
            .ToArray();

        return ExecuteAsync(
            "replace",
            () => gateway.ReplaceAsync(
                source.DocumentKey,
                source.Version,
                documents,
                cancellationToken),
            documents.Length,
            cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeEvidence>> SearchAsync(
        string query,
        float[] queryEmbedding,
        Guid customerId,
        Guid contractId,
        DateOnly asOf,
        int limit,
        CancellationToken cancellationToken = default)
    {
        int candidateCount = Math.Max(limit * 10, 20);
        IReadOnlyList<AzureSearchHit> hits = await ExecuteAsync(
            "hybrid_search",
            () => gateway.HybridSearchAsync(
                new AzureSearchHybridQuery(
                    query,
                    queryEmbedding,
                    customerId,
                    contractId,
                    asOf,
                    candidateCount),
                cancellationToken),
            result => result.Count,
            cancellationToken);

        return hits
            .Select(MapEvidence)
            .Take(limit)
            .ToArray();
    }

    private static AzureSearchKnowledgeChunkDocument MapDocument(
        KnowledgeDocumentSource source,
        string contentChecksum,
        string embeddingModel,
        KnowledgeChunk chunk)
    {
        Guid chunkId = CreateChunkId(source.DocumentKey, source.Version, chunk.Index);

        return new AzureSearchKnowledgeChunkDocument
        {
            Id = chunkId.ToString("N"),
            SchemaVersion = AzureSearchGateway.SchemaVersion,
            DocumentKey = source.DocumentKey,
            Title = source.Title,
            DocumentType = source.DocumentType.ToString(),
            Version = source.Version,
            Language = source.Language,
            CustomerId = source.CustomerId?.ToString("D"),
            ContractId = source.ContractId?.ToString("D"),
            EffectiveFrom = ToUtcDateTimeOffset(source.EffectiveFrom),
            EffectiveTo = source.EffectiveTo is { } effectiveTo
                ? ToUtcDateTimeOffset(effectiveTo)
                : null,
            SourcePath = source.SourcePath,
            ContentChecksum = contentChecksum,
            EmbeddingModel = embeddingModel,
            ChunkIndex = chunk.Index,
            Section = chunk.Section,
            Page = chunk.Page,
            Content = chunk.Content,
            ChunkChecksum = chunk.Checksum,
            Embedding = chunk.Embedding,
        };
    }

    private static KnowledgeEvidence MapEvidence(AzureSearchHit hit)
    {
        AzureSearchKnowledgeChunkDocument document = hit.Document;

        return new KnowledgeEvidence(
            Guid.ParseExact(document.Id, "N"),
            document.DocumentKey,
            document.Title,
            Enum.Parse<KnowledgeDocumentType>(document.DocumentType),
            document.Version,
            document.Language,
            ParseOptionalGuid(document.CustomerId),
            ParseOptionalGuid(document.ContractId),
            DateOnly.FromDateTime(document.EffectiveFrom.UtcDateTime),
            document.SourcePath,
            document.Section,
            document.Page,
            document.Content,
            hit.Score,
            LexicalScore: null,
            VectorScore: null);
    }

    private static Guid CreateChunkId(
        string documentKey,
        string version,
        int chunkIndex)
    {
        byte[] source = Encoding.UTF8.GetBytes(
            $"{AzureSearchGateway.SchemaVersion}:{documentKey}:{version}:{chunkIndex}");
        byte[] hash = SHA256.HashData(source);
        return new Guid(hash.AsSpan(0, 16));
    }

    private static DateTimeOffset ToUtcDateTimeOffset(DateOnly value) =>
        new(value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static Guid? ParseOptionalGuid(string? value) =>
        value is null ? null : Guid.Parse(value);

    private static async Task ExecuteAsync(
        string operation,
        Func<Task> action,
        int resultCount,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            operation,
            async () =>
            {
                await action();
                return true;
            },
            _ => resultCount,
            cancellationToken);
    }

    private static Task<T> ExecuteAsync<T>(
        string operation,
        Func<Task<T>> action,
        int resultCount,
        CancellationToken cancellationToken) =>
        ExecuteAsync(operation, action, _ => resultCount, cancellationToken);

    private static async Task<T> ExecuteAsync<T>(
        string operation,
        Func<Task<T>> action,
        Func<T, int> resultCount,
        CancellationToken cancellationToken)
    {
        long startedAt = Stopwatch.GetTimestamp();
        using Activity? activity = ContractIqTelemetry.StartActivity(
            $"contractiq.knowledge.index.{operation}");
        activity?.SetTag("db.system.name", "azure_ai_search");
        activity?.SetTag("db.operation.name", operation);

        try
        {
            T result = await action();
            int count = resultCount(result);
            activity?.SetTag("contractiq.knowledge.result.count", count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            ContractIqTelemetry.RecordKnowledgeIndexDependency(
                ProviderName,
                operation,
                "succeeded",
                Stopwatch.GetElapsedTime(startedAt),
                count);
            return result;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            RecordFailure(activity, operation, "cancelled", exception, startedAt);
            throw;
        }
        catch (OperationCanceledException exception)
        {
            RecordFailure(activity, operation, "unavailable", exception, startedAt);
            throw CreateUnavailableException(exception);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            RecordFailure(activity, operation, "unavailable", exception, startedAt);
            throw CreateUnavailableException(exception);
        }
        catch (Exception exception)
        {
            RecordFailure(activity, operation, "failed", exception, startedAt);
            throw;
        }
    }

    private static bool IsUnavailable(Exception exception) =>
        exception is RequestFailedException or
            AuthenticationFailedException or
            CredentialUnavailableException or
            HttpRequestException;

    private static ExternalDependencyUnavailableException CreateUnavailableException(
        Exception exception) =>
        new(
            "azure-ai-search",
            "Azure AI Search is unavailable or the configured index cannot be accessed.",
            exception);

    private static void RecordFailure(
        Activity? activity,
        string operation,
        string outcome,
        Exception exception,
        long startedAt)
    {
        ContractIqTelemetry.MarkError(activity, exception);
        ContractIqTelemetry.RecordKnowledgeIndexDependency(
            ProviderName,
            operation,
            outcome,
            Stopwatch.GetElapsedTime(startedAt),
            resultCount: 0);
    }
}
