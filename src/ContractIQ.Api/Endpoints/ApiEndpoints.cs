using System.ComponentModel.DataAnnotations;
using ContractIQ.Application.Assistant;
using ContractIQ.Application.Cancellations.CreateCancellationRequest;
using ContractIQ.Application.Contracts.AssessCancellation;
using ContractIQ.Application.Contracts.GetContractDetails;
using ContractIQ.Application.Contracts.ListCustomerContracts;
using ContractIQ.Application.Customers.ListCustomers;
using ContractIQ.Application.Knowledge;
using ContractIQ.Application.Knowledge.Search;
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

        api.MapPost("/knowledge/search", SearchKnowledgeAsync)
            .WithName("SearchKnowledge")
            .WithTags("Knowledge")
            .WithSummary("Searches contract and policy evidence using local hybrid retrieval.")
            .WithDescription(
                "Applies customer and contract scope before fusing PostgreSQL lexical and vector rankings.")
            .Produces<KnowledgeEvidence[]>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        api.MapPost("/assistant/answers", AskContractQuestionAsync)
            .WithName("AskContractQuestion")
            .WithTags("Assistant")
            .WithSummary("Answers a contract question with deterministic assessment and citations.")
            .WithDescription(
                "Retrieval is read-only and untrusted. Eligibility, dates, and penalties come from domain rules.")
            .Produces<ContractAnswer>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

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

    private static async Task<IResult> SearchKnowledgeAsync(
        SearchKnowledgeRequest request,
        IKnowledgeSearch handler,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<KnowledgeEvidence> evidence = await handler.HandleAsync(
            new SearchKnowledgeQuery(
                request.Query,
                request.CustomerId,
                request.ContractId,
                request.AsOf,
                request.Limit ?? 5),
            cancellationToken);

        return Results.Ok(evidence);
    }

    private static async Task<IResult> AskContractQuestionAsync(
        AskContractQuestionRequest request,
        AskContractQuestionHandler handler,
        CancellationToken cancellationToken)
    {
        ContractAnswer answer = await handler.HandleAsync(
            new AskContractQuestionCommand(
                request.Question,
                request.CustomerId,
                request.ContractId,
                request.Language),
            cancellationToken);

        return Results.Ok(answer);
    }

    private sealed record SearchKnowledgeRequest(
        string Query,
        Guid CustomerId,
        Guid ContractId,
        DateOnly? AsOf,
        int? Limit);

    private sealed record AskContractQuestionRequest(
        string Question,
        Guid CustomerId,
        Guid ContractId,
        string Language);
}
