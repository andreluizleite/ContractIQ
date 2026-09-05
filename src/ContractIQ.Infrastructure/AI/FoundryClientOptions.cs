using Microsoft.Extensions.Configuration;

namespace ContractIQ.Infrastructure.AI;

public sealed record FoundryClientOptions(int MaximumRetries)
{
    public const int DefaultMaximumRetries = 3;

    public static FoundryClientOptions Default => new(DefaultMaximumRetries);

    public static FoundryClientOptions FromConfiguration(IConfiguration configuration)
    {
        string? value = configuration["Foundry:MaximumRetries"];
        int maximumRetries = string.IsNullOrWhiteSpace(value)
            ? DefaultMaximumRetries
            : int.TryParse(value, out int configuredRetries)
                ? configuredRetries
                : -1;

        if (maximumRetries is < 0 or > 5)
        {
            throw new InvalidOperationException(
                "Foundry maximum retries must be an integer between 0 and 5.");
        }

        return new FoundryClientOptions(maximumRetries);
    }
}
