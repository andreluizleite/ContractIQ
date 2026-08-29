using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContractIQ.AiEvaluator;

public static class EvaluationJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static async Task<T> ReadAsync<T>(
        string path,
        CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.OpenRead(path);
        T? value = await JsonSerializer.DeserializeAsync<T>(
            stream,
            Options,
            cancellationToken);

        return value ?? throw new InvalidDataException(
            $"Evaluation file '{path}' is empty or invalid.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
