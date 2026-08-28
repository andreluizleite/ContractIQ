using System.Data;
using ContractIQ.Application.Knowledge;
using ContractIQ.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Pgvector;

namespace ContractIQ.Infrastructure.Persistence;

internal sealed class PostgresKnowledgeIndex(
    ContractIqDbContext dbContext,
    TimeProvider timeProvider) : IKnowledgeIndex
{
    private const int ReciprocalRankConstant = 60;

    public Task<bool> IsCurrentAsync(
        string documentKey,
        string version,
        string contentChecksum,
        string embeddingModel,
        CancellationToken cancellationToken = default)
    {
        return dbContext.KnowledgeDocuments.AnyAsync(
            document =>
                document.DocumentKey == documentKey &&
                document.Version == version &&
                document.ContentChecksum == contentChecksum &&
                document.EmbeddingModel == embeddingModel,
            cancellationToken);
    }

    public async Task ReplaceAsync(
        KnowledgeDocumentSource source,
        string contentChecksum,
        string embeddingModel,
        IReadOnlyList<KnowledgeChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        KnowledgeDocumentRecord? existing = await dbContext.KnowledgeDocuments
            .Include(document => document.Chunks)
            .SingleOrDefaultAsync(
                document => document.DocumentKey == source.DocumentKey &&
                    document.Version == source.Version,
                cancellationToken);

        if (existing is not null)
        {
            dbContext.KnowledgeDocuments.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var document = new KnowledgeDocumentRecord
        {
            Id = Guid.NewGuid(),
            DocumentKey = source.DocumentKey,
            Title = source.Title,
            DocumentType = source.DocumentType,
            Version = source.Version,
            Language = source.Language,
            CustomerId = source.CustomerId,
            ContractId = source.ContractId,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            SourcePath = source.SourcePath,
            ContentChecksum = contentChecksum,
            EmbeddingModel = embeddingModel,
            IndexedAtUtc = timeProvider.GetUtcNow(),
        };

        document.Chunks.AddRange(chunks.Select(chunk => new KnowledgeChunkRecord
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            ChunkIndex = chunk.Index,
            Section = chunk.Section,
            Page = chunk.Page,
            Content = chunk.Content,
            ContentChecksum = chunk.Checksum,
            Embedding = new Vector(chunk.Embedding),
        }));

        dbContext.KnowledgeDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
        const string sql = """
            WITH versioned_documents AS (
                SELECT d.*,
                       ROW_NUMBER() OVER (
                           PARTITION BY d.document_key
                           ORDER BY d.effective_from DESC, d.version DESC) AS version_rank
                FROM knowledge_documents d
                WHERE d.effective_from <= @as_of
                  AND (d.effective_to IS NULL OR d.effective_to >= @as_of)
                  AND (d.customer_id IS NULL OR d.customer_id = @customer_id)
                  AND (d.contract_id IS NULL OR d.contract_id = @contract_id)
            ),
            eligible_chunks AS (
                SELECT c.*, d.document_key, d.title, d.document_type, d.version,
                       d.language, d.customer_id, d.contract_id, d.effective_from,
                       d.source_path
                FROM knowledge_chunks c
                INNER JOIN versioned_documents d ON d.id = c.document_id
                WHERE d.version_rank = 1
            ),
            lexical AS (
                SELECT id,
                       ts_rank_cd(search_vector, websearch_to_tsquery('simple', @query))::double precision AS score,
                       ROW_NUMBER() OVER (
                           ORDER BY ts_rank_cd(search_vector, websearch_to_tsquery('simple', @query)) DESC) AS rank
                FROM eligible_chunks
                WHERE search_vector @@ websearch_to_tsquery('simple', @query)
                ORDER BY score DESC
                LIMIT @candidate_count
            ),
            semantic AS (
                SELECT id,
                       (1 - (embedding <=> @embedding))::double precision AS score,
                       ROW_NUMBER() OVER (ORDER BY embedding <=> @embedding) AS rank
                FROM eligible_chunks
                ORDER BY embedding <=> @embedding
                LIMIT @candidate_count
            ),
            fused AS (
                SELECT COALESCE(lexical.id, semantic.id) AS id,
                       COALESCE(1.0::double precision / (@rrf_constant + lexical.rank), 0.0) +
                       COALESCE(1.0::double precision / (@rrf_constant + semantic.rank), 0.0) AS score,
                       lexical.score AS lexical_score,
                       semantic.score AS vector_score
                FROM lexical
                FULL OUTER JOIN semantic ON semantic.id = lexical.id
            )
            SELECT c.id, c.document_key, c.title, c.document_type, c.version,
                   c.language, c.customer_id, c.contract_id, c.effective_from,
                   c.source_path, c.section, c.page, c.content, f.score,
                   f.lexical_score, f.vector_score
            FROM fused f
            INNER JOIN eligible_chunks c ON c.id = f.id
            ORDER BY f.score DESC, c.document_key, c.chunk_index
            LIMIT @limit;
            """;

        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        bool closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("query", query);
            command.Parameters.AddWithValue("embedding", new Vector(queryEmbedding));
            command.Parameters.AddWithValue("customer_id", customerId);
            command.Parameters.AddWithValue("contract_id", contractId);
            command.Parameters.AddWithValue("as_of", asOf);
            command.Parameters.AddWithValue("candidate_count", Math.Max(limit * 10, 20));
            command.Parameters.AddWithValue("rrf_constant", ReciprocalRankConstant);
            command.Parameters.AddWithValue("limit", limit);

            var evidence = new List<KnowledgeEvidence>();
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                evidence.Add(new KnowledgeEvidence(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    Enum.Parse<KnowledgeDocumentType>(reader.GetString(3)),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetGuid(6),
                    reader.IsDBNull(7) ? null : reader.GetGuid(7),
                    reader.GetFieldValue<DateOnly>(8),
                    reader.GetString(9),
                    reader.GetString(10),
                    reader.GetInt32(11),
                    reader.GetString(12),
                    reader.GetDouble(13),
                    reader.IsDBNull(14) ? null : reader.GetDouble(14),
                    reader.IsDBNull(15) ? null : reader.GetDouble(15)));
            }

            return evidence;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}
