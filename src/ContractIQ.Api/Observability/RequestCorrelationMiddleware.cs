using System.Diagnostics;

namespace ContractIQ.Api.Observability;

public sealed class RequestCorrelationMiddleware(
    RequestDelegate next,
    ILogger<RequestCorrelationMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // The scope lets every structured log emitted during this request carry
        // the same identifier as the trace returned to the caller.
        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
        });

        await next(context);
    }

    public static string ResolveCorrelationId(HttpContext context)
    {
        if (Activity.Current is { TraceId: var traceId } &&
            traceId != default)
        {
            return traceId.ToString();
        }

        if (ActivityContext.TryParse(
            context.TraceIdentifier,
            traceState: null,
            out ActivityContext activityContext))
        {
            return activityContext.TraceId.ToString();
        }

        return context.TraceIdentifier;
    }
}
