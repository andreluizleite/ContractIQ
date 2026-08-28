using System.Text;

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
        IReadOnlyList<KnowledgeDocumentSource> documents =
            await catalog.ReadAllAsync(cancellationToken);

        int indexedDocuments = 0;
        int skippedDocuments = 0;
        int indexedChunks = 0;

        foreach (KnowledgeDocumentSource document in documents)
        {
            string checksum = ComputeDocumentChecksum(document);
            bool isCurrent = await knowledgeIndex.IsCurrentAsync(
                document.DocumentKey,
                document.Version,
                checksum,
                embeddingGenerator.ModelId,
                cancellationToken);

            if (isCurrent)
            {
                skippedDocuments++;
                continue;
            }

            IReadOnlyList<KnowledgeChunkDraft> drafts = chunker.Chunk(document.Content);
            IReadOnlyList<float[]> embeddings = await embeddingGenerator.GenerateAsync(
                drafts.Select(chunk => chunk.Content).ToArray(),
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

            await knowledgeIndex.ReplaceAsync(
                document,
                checksum,
                embeddingGenerator.ModelId,
                chunks,
                cancellationToken);

            indexedDocuments++;
            indexedChunks += chunks.Length;
        }

        return new IndexKnowledgeDocumentsResult(
            indexedDocuments,
            skippedDocuments,
            indexedChunks);
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
