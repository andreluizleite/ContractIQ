using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ContractIQ.Api.Health;

public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext httpContext, HealthReport report)
    {
        var response = new
        {
            status = report.Status.ToString(),
            totalDurationMilliseconds = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMilliseconds = entry.Value.Duration.TotalMilliseconds,
            }),
        };

        return httpContext.Response.WriteAsJsonAsync(
            response,
            cancellationToken: httpContext.RequestAborted);
    }
}
