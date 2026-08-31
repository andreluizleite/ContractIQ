using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace ContractIQ.Infrastructure.Knowledge;

public enum KnowledgeIndexProvider
{
    PostgreSql,
    AzureAiSearch,
}

public sealed partial record KnowledgeIndexOptions(
    KnowledgeIndexProvider Provider,
    Uri? AzureSearchEndpoint,
    string AzureSearchIndexName,
    int MaximumRetries = 3)
{
    public const string DefaultAzureSearchIndexName = "contractiq-knowledge-v1";
    public const int DefaultMaximumRetries = 3;

    public static KnowledgeIndexOptions Local =>
        new(KnowledgeIndexProvider.PostgreSql, null, DefaultAzureSearchIndexName);

    public static KnowledgeIndexOptions FromConfiguration(IConfiguration configuration)
    {
        string providerValue = configuration["Knowledge:IndexProvider"] ?? "PostgreSql";
        if (!Enum.TryParse(
            providerValue,
            ignoreCase: true,
            out KnowledgeIndexProvider provider))
        {
            throw new InvalidOperationException(
                $"Knowledge index provider '{providerValue}' is not supported. " +
                "Use 'PostgreSql' or 'AzureAiSearch'.");
        }

        if (provider == KnowledgeIndexProvider.PostgreSql)
        {
            return Local;
        }

        string endpoint = GetRequiredSetting(
            configuration,
            "AzureSearch:Endpoint",
            "Azure AI Search is selected, but no endpoint is configured.");
        string indexName = configuration["AzureSearch:IndexName"]?.Trim()
            ?? DefaultAzureSearchIndexName;
        int maximumRetries = ParseMaximumRetries(
            configuration["AzureSearch:MaximumRetries"]);
        var endpointUri = new Uri(endpoint, UriKind.Absolute);

        if (endpointUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Azure AI Search endpoint must use HTTPS.");
        }

        if (!AzureSearchIndexNamePattern().IsMatch(indexName))
        {
            throw new InvalidOperationException(
                "Azure AI Search index name must contain 2 to 128 lowercase letters, " +
                "digits, hyphens, or underscores and start and end with a letter or digit.");
        }

        return new KnowledgeIndexOptions(
            provider,
            endpointUri,
            indexName,
            maximumRetries);
    }

    private static int ParseMaximumRetries(string? value)
    {
        int maximumRetries = string.IsNullOrWhiteSpace(value)
            ? DefaultMaximumRetries
            : int.TryParse(value, out int configuredRetries)
                ? configuredRetries
                : -1;

        if (maximumRetries is < 0 or > 5)
        {
            throw new InvalidOperationException(
                "Azure AI Search maximum retries must be an integer between 0 and 5.");
        }

        return maximumRetries;
    }

    private static string GetRequiredSetting(
        IConfiguration configuration,
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

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,126}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex AzureSearchIndexNamePattern();
}
