using ContractIQ.Application.Common.Observability;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ContractIQ.Api.Observability;

public static class OpenTelemetryExtensions
{
    public static WebApplicationBuilder AddContractIqOpenTelemetry(
        this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        OpenTelemetryOptions options = OpenTelemetryOptions.FromConfiguration(
            builder.Configuration);

        if (!options.Enabled)
        {
            return builder;
        }

        string serviceVersion = typeof(Program).Assembly
            .GetName()
            .Version?
            .ToString() ?? "unknown";
        ResourceBuilder resource = ResourceBuilder
            .CreateDefault()
            .AddService(options.ServiceName, serviceVersion: serviceVersion);

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resourceBuilder =>
                resourceBuilder.AddService(options.ServiceName, serviceVersion: serviceVersion))
            .WithTracing(tracing => tracing
                .AddSource(ContractIqTelemetry.ActivitySourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddProcessor(new HttpRoutePrivacyProcessor())
                .AddOtlpExporter(exporter => exporter.Endpoint = options.OtlpEndpoint))
            .WithMetrics(metrics => metrics
                .SetExemplarFilter(ExemplarFilterType.TraceBased)
                .AddMeter(ContractIqTelemetry.MeterName)
                .AddNpgsqlInstrumentation(_ => { })
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(exporter => exporter.Endpoint = options.OtlpEndpoint));

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.SetResourceBuilder(resource);
            logging.AddOtlpExporter(exporter => exporter.Endpoint = options.OtlpEndpoint);
        });

        return builder;
    }
}
