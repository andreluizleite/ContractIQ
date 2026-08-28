using System.ComponentModel.DataAnnotations;
using ContractIQ.Application.Cancellations.CreateCancellationRequest;
using ContractIQ.Application.Contracts.AssessCancellation;
using ContractIQ.Application.Contracts.GetContractDetails;
using ContractIQ.Application.Contracts.ListCustomerContracts;
using ContractIQ.Application.Customers.ListCustomers;
using Microsoft.AspNetCore.Mvc;

namespace ContractIQ.Api.Endpoints;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1");

        api.MapGet("/customers", ListCustomersAsync)
            .WithName("ListCustomers")
            .WithTags("Customers")
            .WithSummary("Lists customers available to the current user.")
            .Produces<CustomerSummaryDto[]>(StatusCodes.Status200OK);

        api.MapGet("/customers/{customerId:guid}/contracts", ListCustomerContractsAsync)
            .WithName("ListCustomerContracts")
            .WithTags("Contracts")
            .WithSummary("Lists the structured contracts for a customer.")
            .Produces<ContractSummaryDto[]>(StatusCodes.Status200OK);

        api.MapGet("/contracts/{contractId:guid}", GetContractDetailsAsync)
            .WithName("GetContractDetails")
            .WithTags("Contracts")
            .WithSummary("Gets structured contract details.")
            .Produces<ContractDetailsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet(
                "/contracts/{contractId:guid}/cancellation-assessment",
                AssessCancellationAsync)
            .WithName("AssessContractCancellation")
            .WithTags("Contracts")
            .WithSummary("Calculates cancellation eligibility, effective date, and penalty.")
            .WithDescription("The result is calculated by deterministic domain rules using the server clock.")
            .Produces<CancellationAssessmentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        api.MapPost(
                "/contracts/{contractId:guid}/cancellation-requests",
                CreateCancellationRequestAsync)
            .WithName("CreateCancellationRequest")
            .WithTags("Cancellation Requests")
            .WithSummary("Creates a cancellation request for review.")
            .WithDescription(
                "Requires an Idempotency-Key header. Penalty, dates, and status are calculated by the server.")
            .Produces<CancellationRequestDto>(StatusCodes.Status201Created)
            .Produces<CancellationRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> ListCustomersAsync(
        ListCustomersHandler handler,
        CancellationToken cancellationToken)
    {
        var customers = await handler.HandleAsync(new ListCustomersQuery(), cancellationToken);

        return Results.Ok(customers);
    }

    private static async Task<IResult> GetContractDetailsAsync(
        Guid contractId,
        GetContractDetailsHandler handler,
        CancellationToken cancellationToken)
    {
        var contract = await handler.HandleAsync(
            new GetContractDetailsQuery(contractId),
            cancellationToken);

        return Results.Ok(contract);
    }

    private static async Task<IResult> ListCustomerContractsAsync(
        Guid customerId,
        ListCustomerContractsHandler handler,
        CancellationToken cancellationToken)
    {
        var contracts = await handler.HandleAsync(
            new ListCustomerContractsQuery(customerId),
            cancellationToken);

        return Results.Ok(contracts);
    }

    private static async Task<IResult> AssessCancellationAsync(
        Guid contractId,
        AssessCancellationHandler handler,
        CancellationToken cancellationToken)
    {
        var assessment = await handler.HandleAsync(
            new AssessCancellationQuery(contractId),
            cancellationToken);

        return Results.Ok(assessment);
    }

    private static async Task<IResult> CreateCancellationRequestAsync(
        Guid contractId,
        [Required]
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CreateCancellationRequestHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateCancellationRequestCommand(contractId, idempotencyKey ?? string.Empty),
            cancellationToken);

        return result.IsReplay
            ? Results.Ok(result.Request)
            : Results.Json(result.Request, statusCode: StatusCodes.Status201Created);
    }
}
