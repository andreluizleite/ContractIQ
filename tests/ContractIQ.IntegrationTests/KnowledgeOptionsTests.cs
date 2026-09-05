using ContractIQ.Application.Knowledge;
using ContractIQ.Infrastructure;
using ContractIQ.Infrastructure.Assistant;
using ContractIQ.Infrastructure.Knowledge;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ContractIQ.IntegrationTests;

public sealed class KnowledgeOptionsTests
{
    [Fact]
    public void Defaults_to_local_ollama_embeddings()
    {
        IConfiguration configuration = BuildConfiguration([]);

        KnowledgeOptions options = KnowledgeOptions.FromConfiguration(configuration);

        Assert.Equal(KnowledgeEmbeddingProvider.Ollama, options.EmbeddingProvider);
        Assert.Equal(new Uri("http://localhost:11434"), options.EmbeddingEndpoint);
        Assert.Equal("embeddinggemma", options.EmbeddingModel);
        Assert.Equal(768, options.EmbeddingDimensions);
    }

    [Fact]
    public void Configures_foundry_embeddings_with_validated_dimensions()
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Knowledge:EmbeddingProvider"] = "Foundry",
                ["Foundry:OpenAIEndpoint"] =
                    "https://aif-contractiq-dev.example/openai/v1/",
                ["Foundry:EmbeddingDeployment"] = "contractiq-embeddings",
                ["Foundry:EmbeddingDimensions"] = "768",
            });

        KnowledgeOptions options = KnowledgeOptions.FromConfiguration(configuration);

        Assert.Equal(KnowledgeEmbeddingProvider.Foundry, options.EmbeddingProvider);
        Assert.Equal(
            new Uri("https://aif-contractiq-dev.example/openai/v1/"),
            options.EmbeddingEndpoint);
        Assert.Equal("contractiq-embeddings", options.EmbeddingModel);
        Assert.Equal(768, options.EmbeddingDimensions);
    }

    [Fact]
    public void Rejects_foundry_without_explicit_embedding_dimensions()
    {
        IConfiguration configuration = BuildConfiguration(
            FoundryConfigurationWithoutDimensions());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => KnowledgeOptions.FromConfiguration(configuration));

        Assert.Contains("dimensions", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_foundry_without_an_embedding_deployment()
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Knowledge:EmbeddingProvider"] = "Foundry",
                ["Foundry:OpenAIEndpoint"] =
                    "https://aif-contractiq-dev.example/openai/v1/",
                ["Foundry:EmbeddingDimensions"] = "768",
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => KnowledgeOptions.FromConfiguration(configuration));

        Assert.Contains(
            "embedding deployment",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_foundry_without_the_openai_v1_endpoint()
    {
        Dictionary<string, string?> values = FoundryConfigurationWithoutDimensions();
        values["Foundry:OpenAIEndpoint"] = "https://aif-contractiq-dev.example/";
        values["Foundry:EmbeddingDimensions"] = "768";
        IConfiguration configuration = BuildConfiguration(values);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => KnowledgeOptions.FromConfiguration(configuration));

        Assert.Contains("/openai/v1/", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_foundry_dimensions_that_do_not_match_the_pgvector_schema()
    {
        Dictionary<string, string?> values = FoundryConfigurationWithoutDimensions();
        values["Foundry:EmbeddingDimensions"] = "1536";
        IConfiguration configuration = BuildConfiguration(values);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => KnowledgeOptions.FromConfiguration(configuration));

        Assert.Contains("768-dimension", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dependency_injection_selects_foundry_without_calling_a_model()
    {
        var knowledgeOptions = new KnowledgeOptions(
            "sample-data/knowledge",
            KnowledgeEmbeddingProvider.Foundry,
            new Uri("https://aif-contractiq-dev.example/openai/v1/"),
            "contractiq-embeddings",
            768);
        var assistantOptions = new AssistantOptions(
            AssistantProvider.Ollama,
            new Uri("http://localhost:11434"),
            "qwen3:4b",
            null,
            600,
            0.1f);
        var services = new ServiceCollection();
        services.AddInfrastructure(
            "Host=localhost;Database=contractiq;Username=test;Password=test",
            knowledgeOptions,
            assistantOptions);

        using ServiceProvider provider = services.BuildServiceProvider();
        IKnowledgeEmbeddingGenerator generator = provider
            .GetRequiredService<IKnowledgeEmbeddingGenerator>();

        Assert.Equal("contractiq-embeddings", generator.ModelId);
        Assert.Equal(768, generator.Dimensions);
    }

    private static Dictionary<string, string?> FoundryConfigurationWithoutDimensions() =>
        new()
        {
            ["Knowledge:EmbeddingProvider"] = "Foundry",
            ["Foundry:OpenAIEndpoint"] =
                "https://aif-contractiq-dev.example/openai/v1/",
            ["Foundry:EmbeddingDeployment"] = "contractiq-embeddings",
        };

    private static IConfiguration BuildConfiguration(
        IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
