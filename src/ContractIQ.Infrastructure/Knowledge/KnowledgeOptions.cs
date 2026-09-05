namespace ContractIQ.Infrastructure.Knowledge;

public enum KnowledgeEmbeddingProvider
{
    Ollama,
    Foundry,
}

public sealed record KnowledgeOptions(
    string ContentRoot,
    KnowledgeEmbeddingProvider EmbeddingProvider,
    Uri EmbeddingEndpoint,
    string EmbeddingModel,
    int EmbeddingDimensions)
{
    public const int StoredEmbeddingDimensions = 768;

    public static KnowledgeOptions FromConfiguration(
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        string contentRoot = configuration["Knowledge:ContentRoot"]
            ?? "sample-data/knowledge";
        string providerValue = configuration["Knowledge:EmbeddingProvider"] ?? "Ollama";

        if (!Enum.TryParse(
            providerValue,
            ignoreCase: true,
            out KnowledgeEmbeddingProvider provider))
        {
            throw new InvalidOperationException(
                $"Knowledge embedding provider '{providerValue}' is not supported. " +
                "Use 'Ollama' or 'Foundry'.");
        }

        string endpoint;
        string model;
        int dimensions;

        if (provider == KnowledgeEmbeddingProvider.Foundry)
        {
            endpoint = GetRequiredSetting(
                configuration,
                "Foundry:OpenAIEndpoint",
                "Foundry is selected as the embedding provider, but no OpenAI endpoint is configured.");
            model = GetRequiredSetting(
                configuration,
                "Foundry:EmbeddingDeployment",
                "Foundry is selected as the embedding provider, but no embedding deployment is configured.");
            dimensions = ParseRequiredDimensions(
                configuration["Foundry:EmbeddingDimensions"]);
        }
        else
        {
            endpoint = configuration["Knowledge:Ollama:Endpoint"]
                ?? "http://localhost:11434";
            model = configuration["Knowledge:Ollama:EmbeddingModel"]
                ?? "embeddinggemma";
            dimensions = int.TryParse(
                configuration["Knowledge:Ollama:Dimensions"],
                out int configuredDimensions)
                ? configuredDimensions
                : StoredEmbeddingDimensions;
        }

        if (dimensions != StoredEmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"The current knowledge index schema requires " +
                $"{StoredEmbeddingDimensions}-dimension embeddings.");
        }

        var endpointUri = new Uri(endpoint, UriKind.Absolute);
        if (provider == KnowledgeEmbeddingProvider.Foundry &&
            endpointUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Foundry embedding endpoint must use HTTPS.");
        }

        if (provider == KnowledgeEmbeddingProvider.Foundry &&
            !endpointUri.AbsolutePath.TrimEnd('/').EndsWith(
                "/openai/v1",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Foundry OpenAI endpoint must end with '/openai/v1/'.");
        }

        return new KnowledgeOptions(contentRoot, provider, endpointUri, model, dimensions);
    }

    private static int ParseRequiredDimensions(string? value)
    {
        if (!int.TryParse(value, out int dimensions))
        {
            throw new InvalidOperationException(
                "Foundry embedding dimensions must be configured as an integer.");
        }

        return dimensions;
    }

    private static string GetRequiredSetting(
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string key,
        string errorMessage)
    {
        string? value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value.Trim();
    }
}
