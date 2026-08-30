using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace ContractIQ.Infrastructure.Assistant;

public enum AssistantProvider
{
    Ollama,
    Kimi,
}

public sealed class AssistantOptions
{
    public AssistantOptions(
        AssistantProvider provider,
        Uri endpoint,
        string chatModel,
        string? apiKey,
        int maximumOutputTokens,
        float temperature)
    {
        Provider = provider;
        Endpoint = endpoint;
        ChatModel = chatModel;
        ApiKey = apiKey;
        MaximumOutputTokens = maximumOutputTokens;
        Temperature = temperature;
    }

    public AssistantProvider Provider { get; }

    public Uri Endpoint { get; }

    public string ChatModel { get; }

    // A class is used instead of a record so its generated string representation
    // cannot accidentally include the hosted provider credential in logs.
    public string? ApiKey { get; }

    public int MaximumOutputTokens { get; }

    public float Temperature { get; }

    public static AssistantOptions FromConfiguration(IConfiguration configuration)
    {
        string providerValue = configuration["Assistant:Provider"] ?? "Ollama";

        if (!Enum.TryParse(providerValue, ignoreCase: true, out AssistantProvider provider))
        {
            throw new InvalidOperationException(
                $"Assistant provider '{providerValue}' is not supported. Use 'Ollama' or 'Kimi'.");
        }

        string endpoint;
        string model;
        string? apiKey = null;

        if (provider == AssistantProvider.Kimi)
        {
            endpoint = configuration["Assistant:Kimi:Endpoint"]
                ?? "https://api.moonshot.ai/v1";
            model = configuration["Assistant:Kimi:ChatModel"]
                ?? "kimi-k2.6";
            apiKey = configuration["Assistant:Kimi:ApiKey"]
                ?? configuration["MOONSHOT_API_KEY"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Kimi is selected as the assistant provider, but no API key is configured. " +
                    "Set 'Assistant:Kimi:ApiKey' with .NET user secrets or set MOONSHOT_API_KEY.");
            }
        }
        else
        {
            endpoint = configuration["Assistant:Ollama:Endpoint"]
                ?? configuration["Knowledge:Ollama:Endpoint"]
                ?? "http://localhost:11434";
            model = configuration["Assistant:Ollama:ChatModel"]
                ?? "qwen3:4b";
        }

        int maximumOutputTokens = int.TryParse(
            configuration["Assistant:MaximumOutputTokens"],
            out int configuredTokens)
            ? configuredTokens
            : 600;
        float temperature = float.TryParse(
            configuration["Assistant:Temperature"],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float configuredTemperature)
            ? configuredTemperature
            : 0.1f;

        if (maximumOutputTokens is < 100 or > 4_000)
        {
            throw new InvalidOperationException(
                "Assistant maximum output tokens must be between 100 and 4000.");
        }

        if (temperature is < 0 or > 2)
        {
            throw new InvalidOperationException(
                "Assistant temperature must be between 0 and 2.");
        }

        var endpointUri = new Uri(endpoint, UriKind.Absolute);

        if (provider == AssistantProvider.Kimi && endpointUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Kimi endpoint must use HTTPS because the hosted request contains an API key.");
        }

        return new AssistantOptions(
            provider,
            endpointUri,
            model,
            apiKey,
            maximumOutputTokens,
            temperature);
    }
}
