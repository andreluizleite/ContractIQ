namespace ContractIQ.Infrastructure.Knowledge;

public sealed record KnowledgeOptions(
    string ContentRoot,
    Uri OllamaEndpoint,
    string EmbeddingModel,
    int EmbeddingDimensions)
{
    public const int StoredEmbeddingDimensions = 768;

    public static KnowledgeOptions FromConfiguration(
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        string contentRoot = configuration["Knowledge:ContentRoot"]
            ?? "sample-data/knowledge";
        string endpoint = configuration["Knowledge:Ollama:Endpoint"]
            ?? "http://localhost:11434";
        string model = configuration["Knowledge:Ollama:EmbeddingModel"]
            ?? "embeddinggemma";
        int dimensions = int.TryParse(
            configuration["Knowledge:Ollama:Dimensions"],
            out int configuredDimensions)
            ? configuredDimensions
            : StoredEmbeddingDimensions;

        if (dimensions != StoredEmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"The current pgvector schema requires {StoredEmbeddingDimensions}-dimension embeddings.");
        }

        return new KnowledgeOptions(
            contentRoot,
            new Uri(endpoint, UriKind.Absolute),
            model,
            dimensions);
    }
}
