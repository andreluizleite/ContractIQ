using ContractIQ.Infrastructure.Assistant;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ContractIQ.IntegrationTests;

public sealed class AssistantOptionsTests
{
    [Fact]
    public void Defaults_to_local_ollama_without_a_hosted_credential()
    {
        IConfiguration configuration = BuildConfiguration([]);

        AssistantOptions options = AssistantOptions.FromConfiguration(configuration);

        Assert.Equal(AssistantProvider.Ollama, options.Provider);
        Assert.Equal(new Uri("http://localhost:11434"), options.Endpoint);
        Assert.Equal("qwen3:4b", options.ChatModel);
        Assert.Null(options.ApiKey);
    }

    [Fact]
    public void Configures_kimi_from_provider_settings()
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Assistant:Provider"] = "Kimi",
                ["Assistant:Kimi:ApiKey"] = "local-test-key",
            });

        AssistantOptions options = AssistantOptions.FromConfiguration(configuration);

        Assert.Equal(AssistantProvider.Kimi, options.Provider);
        Assert.Equal(new Uri("https://api.moonshot.ai/v1"), options.Endpoint);
        Assert.Equal("kimi-k2.6", options.ChatModel);
        Assert.Equal("local-test-key", options.ApiKey);
    }

    [Fact]
    public void Rejects_kimi_without_an_api_key()
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Assistant:Provider"] = "Kimi",
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => AssistantOptions.FromConfiguration(configuration));

        Assert.Contains("no API key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_a_plaintext_kimi_endpoint()
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Assistant:Provider"] = "Kimi",
                ["Assistant:Kimi:ApiKey"] = "local-test-key",
                ["Assistant:Kimi:Endpoint"] = "http://hosted-provider.example/v1",
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => AssistantOptions.FromConfiguration(configuration));

        Assert.Contains("HTTPS", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Configures_foundry_with_keyless_deployment_settings()
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Assistant:Provider"] = "Foundry",
                ["Foundry:OpenAIEndpoint"] =
                    "https://aif-contractiq-dev.example/openai/v1/",
                ["Foundry:ChatDeployment"] = "contractiq-chat",
            });

        AssistantOptions options = AssistantOptions.FromConfiguration(configuration);

        Assert.Equal(AssistantProvider.Foundry, options.Provider);
        Assert.Equal(
            new Uri("https://aif-contractiq-dev.example/openai/v1/"),
            options.Endpoint);
        Assert.Equal("contractiq-chat", options.ChatModel);
        Assert.Null(options.ApiKey);
    }

    [Fact]
    public void Rejects_foundry_without_a_chat_deployment()
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Assistant:Provider"] = "Foundry",
                ["Foundry:OpenAIEndpoint"] =
                    "https://aif-contractiq-dev.example/openai/v1/",
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => AssistantOptions.FromConfiguration(configuration));

        Assert.Contains("chat deployment", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_foundry_without_the_openai_v1_endpoint()
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Assistant:Provider"] = "Foundry",
                ["Foundry:OpenAIEndpoint"] = "https://aif-contractiq-dev.example/",
                ["Foundry:ChatDeployment"] = "contractiq-chat",
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => AssistantOptions.FromConfiguration(configuration));

        Assert.Contains("/openai/v1/", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_an_unknown_provider()
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Assistant:Provider"] = "Unknown",
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => AssistantOptions.FromConfiguration(configuration));

        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration BuildConfiguration(
        IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
