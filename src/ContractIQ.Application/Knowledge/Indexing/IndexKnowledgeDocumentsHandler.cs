using System.Diagnostics;
using System.Text;
using ContractIQ.Application.Common.Observability;

namespace ContractIQ.Application.Knowledge.Indexing;

public sealed class IndexKnowledgeDocumentsHandler(
    IKnowledgeDocumentCatalog catalog,
    IKnowledgeEmbeddingGenerator embeddingGenerator,
    IKnowledgeIndex knowledgeIndex,
    MarkdownKnowledgeChunker chunker)
{
    public async Task<IndexKnowledgeDocumentsResult> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        long startedAt = Stopwatch.GetTimestamp();
        int indexedDocuments = 0;
        int skippedDocuments = 0;
        int indexedChunks = 0;
        using Activity? activity = ContractIqTelemetry.StartActivity(
            "contractiq.knowledge.index");

        try
        {
            IReadOnlyList<KnowledgeDocumentSource> documents =
                await catalog.ReadAllAsync(cancellationToken);
            activity?.SetTag("contractiq.knowledge.document.discovered.count", documents.Count);

            foreach (KnowledgeDocumentSource document in documents)
            {
                using Activity? documentActivity = ContractIqTelemetry.StartActivity(
                    "contractiq.knowledge.document.index");
                documentActivity?.SetTag(
                    "contractiq.knowledge.document.type",
                    document.DocumentType.ToString().ToLowerInvariant());
                documentActivity?.SetTag(
                    "contractiq.knowledge.document.language",
                    document.Language);

                try
                {
                    string checksum = ComputeDocumentChecksum(document);
                    bool isCurrent = await IsCurrentAsync(
                        document,
                        checksum,
                        cancellationToken);

                    if (isCurrent)
                    {
                        skippedDocuments++;
                        documentActivity?.SetTag("contractiq.outcome", "skipped");
                        documentActivity?.SetStatus(ActivityStatusCode.Ok);
                        continue;
                    }

                    IReadOnlyList<KnowledgeChunkDraft> drafts = chunker.Chunk(document.Content);
                    IReadOnlyList<float[]> embeddings = await GenerateEmbeddingsAsync(
                        drafts,
                        cancellationToken);

                    if (embeddings.Count != drafts.Count ||
                        embeddings.Any(embedding => embedding.Length != embeddingGenerator.Dimensions))
                    {
                        throw new InvalidOperationException(
                            $"Embedding model '{embeddingGenerator.ModelId}' returned an unexpected shape.");
                    }

                    KnowledgeChunk[] chunks = drafts
                        .Select((draft, index) => new KnowledgeChunk(
                            draft.Index,
                            draft.Section,
                            draft.Page,
                            draft.Content,
                            draft.Checksum,
                            embeddings[index]))
                        .ToArray();

                    await ReplaceAsync(
                        document,
                        checksum,
                        chunks,
                        cancellationToken);

                    indexedDocuments++;
                    indexedChunks += chunks.Length;
                    documentActivity?.SetTag("contractiq.outcome", "indexed");
                    documentActivity?.SetTag(
                        "contractiq.knowledge.chunk.count",
                        chunks.Length);
                    documentActivity?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception exception)
                {
                    ContractIqTelemetry.MarkError(documentActivity, exception);
                    throw;
                }
            }

            activity?.SetTag("contractiq.knowledge.document.indexed.count", indexedDocuments);
            activity?.SetTag("contractiq.knowledge.document.skipped.count", skippedDocuments);
            activity?.SetTag("contractiq.knowledge.chunk.indexed.count", indexedChunks);
            activity?.SetTag("contractiq.outcome", "succeeded");
            activity?.SetStatus(ActivityStatusCode.Ok);
            ContractIqTelemetry.RecordKnowledgeIndexing(
                "succeeded",
                Stopwatch.GetElapsedTime(startedAt),
                indexedDocuments,
                indexedChunks);

            return new IndexKnowledgeDocumentsResult(
                indexedDocuments,
                skippedDocuments,
                indexedChunks);
        }
        catch (Exception exception)
        {
            string outcome = exception is OperationCanceledException
                ? "cancelled"
                : "failed";

            ContractIqTelemetry.MarkError(activity, exception);
            ContractIqTelemetry.RecordKnowledgeIndexing(
                outcome,
                Stopwatch.GetElapsedTime(startedAt),
                indexedDocuments,
                indexedChunks);
            throw;
        }
    }

    private async Task<bool> IsCurrentAsync(
        KnowledgeDocumentSource document,
        string checksum,
        CancellationToken cancellationToken)
    {
        using Activity? activity = ContractIqTelemetry.StartActivity(
            "contractiq.knowledge.index.check");
        activity?.SetTag("gen_ai.request.model", embeddingGenerator.ModelId);

        try
        {
            bool isCurrent = await knowledgeIndex.IsCurrentAsync(
                document.DocumentKey,
                document.Version,
                checksum,
                embeddingGenerator.ModelId,
                cancellationToken);

            activity?.SetTag("contractiq.knowledge.index.current", isCurrent);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return isCurrent;
        }
        catch (Exception exception)
        {
            ContractIqTelemetry.MarkError(activity, exception);
            throw;
        }
    }

    private async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<KnowledgeChunkDraft> drafts,
        CancellationToken cancellationToken)
    {
        using Activity? activity = ContractIqTelemetry.StartActivity(
            "contractiq.knowledge.embedding.generate");
        activity?.SetTag("gen_ai.request.model", embeddingGenerator.ModelId);
        activity?.SetTag("contractiq.knowledge.embedding.input.count", drafts.Count);

        try
        {
            IReadOnlyList<float[]> embeddings = await embeddingGenerator.GenerateAsync(
                drafts.Select(chunk => chunk.Content).ToArray(),
                cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return embeddings;
        }
        catch (Exception exception)
        {
            ContractIqTelemetry.MarkError(activity, exception);
            throw;
        }
    }

    private async Task ReplaceAsync(
        KnowledgeDocumentSource document,
        string checksum,
        IReadOnlyList<KnowledgeChunk> chunks,
        CancellationToken cancellationToken)
    {
        using Activity? activity = ContractIqTelemetry.StartActivity(
            "contractiq.knowledge.index.replace");
        activity?.SetTag("contractiq.knowledge.chunk.count", chunks.Count);

        try
        {
            await knowledgeIndex.ReplaceAsync(
                document,
                checksum,
                embeddingGenerator.ModelId,
                chunks,
                cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception exception)
        {
            ContractIqTelemetry.MarkError(activity, exception);
            throw;
        }
    }

    private static string ComputeDocumentChecksum(KnowledgeDocumentSource document)
    {
        var value = new StringBuilder()
            .AppendLine(document.DocumentKey)
            .AppendLine(document.Version)
            .AppendLine(document.Language)
            .AppendLine(document.EffectiveFrom.ToString("O"))
            .AppendLine(document.EffectiveTo?.ToString("O"))
            .Append(document.Content)
            .ToString();

        return MarkdownKnowledgeChunker.ComputeChecksum(value);
    }
}

public sealed record IndexKnowledgeDocumentsResult(
    int IndexedDocuments,
    int SkippedDocuments,
    int IndexedChunks);
