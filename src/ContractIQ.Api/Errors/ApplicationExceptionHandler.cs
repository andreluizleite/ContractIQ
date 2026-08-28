using System.Diagnostics;
using ContractIQ.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ContractIQ.Api.Errors;

public sealed class ApplicationExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApplicationExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var mapping = MapException(exception);

        if (mapping.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "An unhandled exception occurred while processing the request.");
        }

        var problem = new ProblemDetails
        {
            Status = mapping.Status,
            Title = mapping.Title,
            Detail = mapping.Detail,
            Type = $"urn:contractiq:error:{mapping.Code}",
            Instance = httpContext.Request.Path.Value ?? "/",
        };

        problem.Extensions["code"] = mapping.Code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (mapping.Field is not null)
        {
            problem.Extensions["field"] = mapping.Field;
        }

        httpContext.Response.StatusCode = mapping.Status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ExceptionMapping MapException(Exception exception) => exception switch
    {
        ResourceNotFoundException notFound => new ExceptionMapping(
            StatusCodes.Status404NotFound,
            "Resource not found",
            notFound.Message,
            "resource_not_found"),
        ApplicationConflictException conflict => new ExceptionMapping(
            StatusCodes.Status409Conflict,
            "Request conflict",
            conflict.Message,
            conflict.Code),
        ApplicationValidationException validation => new ExceptionMapping(
            StatusCodes.Status400BadRequest,
            "Validation failed",
            validation.Message,
            "validation_error",
            validation.Field),
        _ => new ExceptionMapping(
            StatusCodes.Status500InternalServerError,
            "Unexpected error",
            "An unexpected error occurred while processing the request.",
            "internal_server_error"),
    };

    private sealed record ExceptionMapping(
        int Status,
        string Title,
        string Detail,
        string Code,
        string? Field = null);
}
