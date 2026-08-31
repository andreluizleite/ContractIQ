using ContractIQ.Application.Knowledge;
using ContractIQ.Infrastructure;
using ContractIQ.Infrastructure.Assistant;
using ContractIQ.Infrastructure.Knowledge;
using ContractIQ.Infrastructure.Knowledge.AzureSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ContractIQ.IntegrationTests;

public sealed class KnowledgeIndexOptionsTests
{
    [Fact]
    public void Defaults_to_local_postgresql_index()
    {
        IConfiguration configuration = BuildConfiguration([]);

        KnowledgeIndexOptions options = KnowledgeIndexOptions.FromConfiguration(configuration);

        Assert.Equal(KnowledgeIndexProvider.PostgreSql, options.Provider);
        Assert.Null(options.AzureSearchEndpoint);
    }

    [Fact]
    public void Configures_keyless_azure_ai_search()
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Knowledge:IndexProvider"] = "AzureAiSearch",
                ["AzureSearch:Endpoint"] =
                    "https://srch-contractiq-dev.example.search.windows.net",
                ["AzureSearch:IndexName"] = "contractiq-knowledge-v1",
            });

        KnowledgeIndexOptions options = KnowledgeIndexOptions.FromConfiguration(configuration);

        Assert.Equal(KnowledgeIndexProvider.AzureAiSearch, options.Provider);
        Assert.Equal(
            new Uri("https://srch-contractiq-dev.example.search.windows.net"),
            options.AzureSearchEndpoint);
        Assert.Equal("contractiq-knowledge-v1", options.AzureSearchIndexName);
    }

    [Fact]
    public void Rejects_azure_ai_search_without_an_endpoint()
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Knowledge:IndexProvider"] = "AzureAiSearch",
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => KnowledgeIndexOptions.FromConfiguration(configuration));

        Assert.Contains("endpoint", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://srch-contractiq-dev.example.search.windows.net", "contractiq-knowledge-v1")]
    [InlineData("https://srch-contractiq-dev.example.search.windows.net", "ContractIQ-Knowledge")]
    public void Rejects_unsafe_endpoint_or_invalid_index_name(
        string endpoint,
        string indexName)
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Knowledge:IndexProvider"] = "AzureAiSearch",
                ["AzureSearch:Endpoint"] = endpoint,
                ["AzureSearch:IndexName"] = indexName,
            });

        Assert.Throws<InvalidOperationException>(
            () => KnowledgeIndexOptions.FromConfiguration(configuration));
    }

    [Fact]
    public void Dependency_injection_selects_azure_search_without_a_network_call()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(
            "Host=localhost;Database=contractiq;Username=test;Password=test",
            new KnowledgeOptions(
                "sample-data/knowledge",
                KnowledgeEmbeddingProvider.Ollama,
                new Uri("http://localhost:11434"),
                "embeddinggemma",
                768),
            new AssistantOptions(
                AssistantProvider.Ollama,
                new Uri("http://localhost:11434"),
                "qwen3:4b",
                null,
                600,
                0.1f),
            new KnowledgeIndexOptions(
                KnowledgeIndexProvider.AzureAiSearch,
                new Uri("https://srch-contractiq-dev.example.search.windows.net"),
                "contractiq-knowledge-v1"));

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IKnowledgeIndex index = scope.ServiceProvider.GetRequiredService<IKnowledgeIndex>();

        Assert.IsType<AzureAiSearchKnowledgeIndex>(index);
    }

    private static IConfiguration BuildConfiguration(
        IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
