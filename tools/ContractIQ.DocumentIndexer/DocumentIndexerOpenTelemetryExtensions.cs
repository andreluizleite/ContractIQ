using ContractIQ.Application.Common.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

internal static class DocumentIndexerOpenTelemetryExtensions
{
    public static HostApplicationBuilder AddDocumentIndexerOpenTelemetry(
        this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        IConfigurationSection section = builder.Configuration.GetSection("OpenTelemetry");
        bool enabled = bool.TryParse(section["Enabled"], out bool configuredEnabled) &&
            configuredEnabled;

        if (!enabled)
        {
            return builder;
        }

        string serviceName = section["ServiceName"]?.Trim() ??
            "ContractIQ.DocumentIndexer";
        string endpointValue = section["OtlpEndpoint"]?.Trim() ??
            "http://localhost:4317";

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new InvalidOperationException(
                "OpenTelemetry:ServiceName must not be empty.");
        }

        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "OpenTelemetry:OtlpEndpoint must be an absolute HTTP or HTTPS URI.");
        }

        string serviceVersion = typeof(Program).Assembly
            .GetName()
            .Version?
            .ToString() ?? "unknown";
        ResourceBuilder resource = ResourceBuilder
            .CreateDefault()
            .AddService(serviceName, serviceVersion: serviceVersion);

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resourceBuilder =>
                resourceBuilder.AddService(serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing => tracing
                .AddSource(ContractIqTelemetry.ActivitySourceName)
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddOtlpExporter(exporter => exporter.Endpoint = endpoint))
            .WithMetrics(metrics => metrics
                .SetExemplarFilter(ExemplarFilterType.TraceBased)
                .AddMeter(ContractIqTelemetry.MeterName)
                .AddNpgsqlInstrumentation(_ => { })
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(exporter => exporter.Endpoint = endpoint));

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.SetResourceBuilder(resource);
            logging.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
        });

        return builder;
    }
}
