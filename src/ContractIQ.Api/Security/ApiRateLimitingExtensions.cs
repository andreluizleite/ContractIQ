using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using ContractIQ.Api.Observability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ContractIQ.Api.Security;

public static class ApiRateLimitingExtensions
{
    public const string AssistantPolicy = "assistant";
    public const string KnowledgePolicy = "knowledge";
    public const string WritePolicy = "write";

    public static IServiceCollection AddContractIqRateLimiting(
        this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(
                AssistantPolicy,
                context => CreatePartition(context, permitLimit: 10));
            options.AddPolicy(
                KnowledgePolicy,
                context => CreatePartition(context, permitLimit: 30));
            options.AddPolicy(
                WritePolicy,
                context => CreatePartition(context, permitLimit: 10));
            options.OnRejected = WriteRejectionAsync;
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(
        HttpContext context,
        int permitLimit)
    {
        string client = context.Connection.RemoteIpAddress?.ToString() ?? "local-test-client";

        return RateLimitPartition.GetFixedWindowLimiter(
            client,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            });
    }

    private static async ValueTask WriteRejectionAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        HttpResponse response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "application/problem+json";

        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out TimeSpan retryAfter))
        {
            response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Request limit exceeded",
            Detail = "Too many requests were received. Wait before trying again.",
            Type = "urn:contractiq:error:rate_limit_exceeded",
            Instance = context.HttpContext.Request.Path.Value ?? "/",
        };
        problem.Extensions["code"] = "rate_limit_exceeded";
        problem.Extensions["traceId"] =
            RequestCorrelationMiddleware.ResolveCorrelationId(context.HttpContext);

        await JsonSerializer.SerializeAsync(
            response.Body,
            problem,
            JsonSerializerOptions.Web,
            cancellationToken);
    }
}
