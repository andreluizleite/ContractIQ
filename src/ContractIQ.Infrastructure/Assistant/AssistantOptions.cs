using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace ContractIQ.Infrastructure.Assistant;

public sealed record AssistantOptions(
    Uri OllamaEndpoint,
    string ChatModel,
    int MaximumOutputTokens,
    float Temperature)
{
    public static AssistantOptions FromConfiguration(IConfiguration configuration)
    {
        string endpoint = configuration["Assistant:Ollama:Endpoint"]
            ?? configuration["Knowledge:Ollama:Endpoint"]
            ?? "http://localhost:11434";
        string model = configuration["Assistant:Ollama:ChatModel"]
            ?? "qwen3:4b";
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

        return new AssistantOptions(
            new Uri(endpoint, UriKind.Absolute),
            model,
            maximumOutputTokens,
            temperature);
    }
}
