namespace ContractIQ.AzureSmokeTest;

internal sealed record AzureSmokeTestReport(
    string Outcome,
    DateTimeOffset StartedAtUtc,
    double DurationMilliseconds,
    string EmbeddingProvider,
    string EmbeddingModel,
    int EmbeddingRequests,
    int EmbeddingInputs,
    int EmbeddingInputCharacters,
    int EmbeddingDimensions,
    int IndexedDocuments,
    int IndexedChunks,
    int SearchQueries,
    int SearchResultCount,
    string SearchProvider,
    string IndexName);
