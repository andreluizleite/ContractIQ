using ContractIQ.Infrastructure.AI;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ContractIQ.IntegrationTests;

public sealed class FoundryClientOptionsTests
{
    [Fact]
    public void Uses_safe_default_retries_for_normal_runtime()
    {
        IConfiguration configuration = BuildConfiguration([]);

        FoundryClientOptions options = FoundryClientOptions.FromConfiguration(configuration);

        Assert.Equal(FoundryClientOptions.DefaultMaximumRetries, options.MaximumRetries);
    }

    [Fact]
    public void Allows_zero_retries_for_the_bounded_smoke_test()
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Foundry:MaximumRetries"] = "0",
            });

        FoundryClientOptions options = FoundryClientOptions.FromConfiguration(configuration);

        Assert.Equal(0, options.MaximumRetries);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("6")]
    [InlineData("many")]
    public void Rejects_unbounded_or_invalid_retry_configuration(string retries)
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Foundry:MaximumRetries"] = retries,
            });

        Assert.Throws<InvalidOperationException>(
            () => FoundryClientOptions.FromConfiguration(configuration));
    }

    private static IConfiguration BuildConfiguration(
        IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
