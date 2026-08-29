using ContractIQ.Api.Observability;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ContractIQ.IntegrationTests;

public sealed class OpenTelemetryOptionsTests
{
    [Fact]
    public void FromConfiguration_keeps_export_opt_in()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:ServiceName"] = "ContractIQ.Tests",
                ["OpenTelemetry:OtlpEndpoint"] = "http://localhost:4317",
            })
            .Build();

        OpenTelemetryOptions options = OpenTelemetryOptions.FromConfiguration(configuration);

        Assert.False(options.Enabled);
        Assert.Equal("ContractIQ.Tests", options.ServiceName);
        Assert.Equal(new Uri("http://localhost:4317"), options.OtlpEndpoint);
    }

    [Fact]
    public void FromConfiguration_rejects_a_non_http_exporter_endpoint()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:Enabled"] = "true",
                ["OpenTelemetry:OtlpEndpoint"] = "file:///tmp/telemetry",
            })
            .Build();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => OpenTelemetryOptions.FromConfiguration(configuration));

        Assert.Contains("absolute HTTP or HTTPS URI", exception.Message);
    }
}
