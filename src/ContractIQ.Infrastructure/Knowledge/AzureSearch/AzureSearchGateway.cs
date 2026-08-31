using Azure;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

namespace ContractIQ.Infrastructure.Knowledge.AzureSearch;

internal sealed class AzureSearchGateway : IAzureSearchGateway, IDisposable
{
    internal const int SchemaVersion = 1;
    internal const string VectorAlgorithmName = "contractiq-hnsw";
    internal const string VectorProfileName = "contractiq-vector-profile";

    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly KnowledgeIndexOptions _options;
    private readonly SemaphoreSlim _indexInitialization = new(1, 1);
    private bool _indexReady;

    public AzureSearchGateway(
        KnowledgeIndexOptions options,
        TokenCredential credential)
    {
        _options = options;
        Uri endpoint = options.AzureSearchEndpoint
            ?? throw new InvalidOperationException(
                "Azure AI Search endpoint is required by the selected index provider.");
        SearchClientOptions clientOptions = CreateClientOptions(options.MaximumRetries);
        _indexClient = new SearchIndexClient(endpoint, credential, clientOptions);
        _searchClient = new SearchClient(
            endpoint,
            options.AzureSearchIndexName,
            credential,
            CreateClientOptions(options.MaximumRetries));
    }

    public async Task<bool> IsCurrentAsync(
        AzureSearchDocumentVersion version,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexAsync(cancellationToken);

        var options = new SearchOptions
        {
            Filter = CreateCurrentVersionFilter(version),
            Size = 1,
        };
        options.Select.Add(nameof(AzureSearchKnowledgeChunkDocument.Id));

        Response<SearchResults<AzureSearchKnowledgeChunkDocument>> response =
            await _searchClient.SearchAsync<AzureSearchKnowledgeChunkDocument>(
                "*",
                options,
                cancellationToken);

        await foreach (SearchResult<AzureSearchKnowledgeChunkDocument> _ in
            response.Value.GetResultsAsync())
        {
            return true;
        }

        return false;
    }

    public async Task ReplaceAsync(
        string documentKey,
        string version,
        IReadOnlyList<AzureSearchKnowledgeChunkDocument> chunks,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexAsync(cancellationToken);

        IReadOnlyList<string> existingKeys = await FindVersionKeysAsync(
            documentKey,
            version,
            cancellationToken);
        HashSet<string> currentKeys = chunks
            .Select(chunk => chunk.Id)
            .ToHashSet(StringComparer.Ordinal);
        string[] staleKeys = existingKeys
            .Where(key => !currentKeys.Contains(key))
            .ToArray();
        var indexingOptions = new IndexDocumentsOptions
        {
            ThrowOnAnyError = true,
        };

        if (staleKeys.Length > 0)
        {
            await _searchClient.DeleteDocumentsAsync(
                nameof(AzureSearchKnowledgeChunkDocument.Id),
                staleKeys,
                indexingOptions,
                cancellationToken);
        }

        if (chunks.Count > 0)
        {
            await _searchClient.MergeOrUploadDocumentsAsync(
                chunks,
                indexingOptions,
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<AzureSearchHit>> HybridSearchAsync(
        AzureSearchHybridQuery query,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexAsync(cancellationToken);

        SearchOptions options = CreateHybridSearchOptions(query);
        Response<SearchResults<AzureSearchKnowledgeChunkDocument>> response =
            await _searchClient.SearchAsync<AzureSearchKnowledgeChunkDocument>(
                query.Text,
                options,
                cancellationToken);

        var hits = new List<AzureSearchHit>();
        await foreach (SearchResult<AzureSearchKnowledgeChunkDocument> result in
            response.Value.GetResultsAsync())
        {
            hits.Add(new AzureSearchHit(result.Document, result.Score ?? 0d));
        }

        return hits;
    }

    public void Dispose() => _indexInitialization.Dispose();

    internal static SearchIndex CreateIndexDefinition(
        string indexName,
        int embeddingDimensions)
    {
        if (embeddingDimensions != KnowledgeOptions.StoredEmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Azure AI Search schema v{SchemaVersion} requires " +
                $"{KnowledgeOptions.StoredEmbeddingDimensions}-dimension embeddings.");
        }

        var fields = new FieldBuilder().Build(
            typeof(AzureSearchKnowledgeChunkDocument));
        var index = new SearchIndex(indexName, fields)
        {
            VectorSearch = new VectorSearch(),
        };
        index.VectorSearch.Algorithms.Add(
            new HnswAlgorithmConfiguration(VectorAlgorithmName));
        index.VectorSearch.Profiles.Add(
            new VectorSearchProfile(VectorProfileName, VectorAlgorithmName));

        return index;
    }

    internal static SearchClientOptions CreateClientOptions(int maximumRetries)
    {
        var options = new SearchClientOptions();
        options.Retry.MaxRetries = maximumRetries;
        return options;
    }

    internal static SearchOptions CreateHybridSearchOptions(
        AzureSearchHybridQuery query)
    {
        var vectorQuery = new VectorizedQuery(query.Vector)
        {
            KNearestNeighborsCount = query.CandidateCount,
        };
        vectorQuery.Fields.Add(nameof(AzureSearchKnowledgeChunkDocument.Embedding));

        var options = new SearchOptions
        {
            Filter = CreateScopeFilter(query),
            Size = query.CandidateCount,
            VectorSearch = new VectorSearchOptions
            {
                FilterMode = VectorFilterMode.PreFilter,
            },
        };
        options.VectorSearch.Queries.Add(vectorQuery);
        AddEvidenceFields(options.Select);

        return options;
    }

    private async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        if (_indexReady)
        {
            return;
        }

        await _indexInitialization.WaitAsync(cancellationToken);
        try
        {
            if (_indexReady)
            {
                return;
            }

            SearchIndex definition = CreateIndexDefinition(
                _options.AzureSearchIndexName,
                KnowledgeOptions.StoredEmbeddingDimensions);
            await _indexClient.CreateOrUpdateIndexAsync(
                definition,
                cancellationToken: cancellationToken);
            _indexReady = true;
        }
        finally
        {
            _indexInitialization.Release();
        }
    }

    private async Task<IReadOnlyList<string>> FindVersionKeysAsync(
        string documentKey,
        string version,
        CancellationToken cancellationToken)
    {
        FormattableString versionFilter = $"""
            SchemaVersion eq {SchemaVersion} and
            DocumentKey eq {documentKey} and Version eq {version}
            """;
        var options = new SearchOptions
        {
            Filter = SearchFilter.Create(versionFilter),
            Size = 1_000,
        };
        options.Select.Add(nameof(AzureSearchKnowledgeChunkDocument.Id));

        Response<SearchResults<AzureSearchKnowledgeChunkDocument>> response =
            await _searchClient.SearchAsync<AzureSearchKnowledgeChunkDocument>(
                "*",
                options,
                cancellationToken);
        var keys = new List<string>();

        await foreach (SearchResult<AzureSearchKnowledgeChunkDocument> result in
            response.Value.GetResultsAsync())
        {
            keys.Add(result.Document.Id);
        }

        return keys;
    }

    private static string CreateCurrentVersionFilter(
        AzureSearchDocumentVersion version)
    {
        FormattableString filter = $"""
            SchemaVersion eq {SchemaVersion} and
            DocumentKey eq {version.DocumentKey} and
            Version eq {version.Version} and
            ContentChecksum eq {version.ContentChecksum} and
            EmbeddingModel eq {version.EmbeddingModel}
            """;
        return SearchFilter.Create(filter);
    }

    private static string CreateScopeFilter(AzureSearchHybridQuery query)
    {
        DateTimeOffset asOf = new(
            query.AsOf.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);
        string customerId = query.CustomerId.ToString("D");
        string contractId = query.ContractId.ToString("D");

        FormattableString filter = $"""
            SchemaVersion eq {SchemaVersion} and
            EffectiveFrom le {asOf} and
            (EffectiveTo eq null or EffectiveTo ge {asOf}) and
            (CustomerId eq null or CustomerId eq {customerId}) and
            (ContractId eq null or ContractId eq {contractId})
            """;
        return SearchFilter.Create(filter);
    }

    private static void AddEvidenceFields(IList<string> select)
    {
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.Id));
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.DocumentKey));
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.Title));
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.DocumentType));
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.Version));
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.Language));
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.CustomerId));
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.ContractId));
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.EffectiveFrom));
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.SourcePath));
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.Section));
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.Page));
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.Content));
        select.Add(nameof(AzureSearchKnowledgeChunkDocument.ChunkIndex));
    }
}
