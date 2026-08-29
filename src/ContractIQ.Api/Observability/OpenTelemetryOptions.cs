namespace ContractIQ.Api.Observability;

public sealed record OpenTelemetryOptions(
    bool Enabled,
    string ServiceName,
    Uri OtlpEndpoint)
{
    public const string SectionName = "OpenTelemetry";

    public static OpenTelemetryOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration.GetSection(SectionName);
        bool enabled = bool.TryParse(section["Enabled"], out bool configuredEnabled) &&
            configuredEnabled;
        string serviceName = section["ServiceName"]?.Trim() ?? "ContractIQ.Api";
        string endpointValue = section["OtlpEndpoint"]?.Trim() ?? "http://localhost:4317";

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new InvalidOperationException(
                $"{SectionName}:ServiceName must not be empty.");
        }

        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                $"{SectionName}:OtlpEndpoint must be an absolute HTTP or HTTPS URI.");
        }

        return new OpenTelemetryOptions(enabled, serviceName, endpoint);
    }
}
