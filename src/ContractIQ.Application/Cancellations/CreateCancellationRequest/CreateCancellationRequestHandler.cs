using System.Diagnostics;
using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Common.Models;
using ContractIQ.Application.Common.Observability;
using ContractIQ.Domain.Cancellations;
using CancellationAssessment = ContractIQ.Domain.Contracts.CancellationAssessment;
using Contract = ContractIQ.Domain.Contracts.Contract;

namespace ContractIQ.Application.Cancellations.CreateCancellationRequest;

public sealed class CreateCancellationRequestHandler(
    IContractRepository contracts,
    ICancellationRequestStore cancellationRequests,
    TimeProvider timeProvider)
{
    public async Task<CreateCancellationRequestResult> HandleAsync(
        CreateCancellationRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        long startedAt = Stopwatch.GetTimestamp();
        using Activity? activity = ContractIqTelemetry.StartActivity(
            "contractiq.cancellation.create");

        try
        {
            ArgumentNullException.ThrowIfNull(command);

            string idempotencyKey = ValidateIdempotencyKey(command.IdempotencyKey);
            DateTimeOffset now = timeProvider.GetUtcNow();

            var replay = await cancellationRequests.FindByIdempotencyKeyAsync(
                idempotencyKey,
                cancellationToken);

            if (replay is not null)
            {
                EnsureReplayMatchesContract(replay, command.ContractId);
                CreateCancellationRequestResult replayResult = ToResult(
                    replay,
                    isReplay: true);
                RecordSuccess(activity, replayResult, startedAt);
                return replayResult;
            }

            var contract = await contracts.GetByIdAsync(command.ContractId, cancellationToken)
                ?? throw new ResourceNotFoundException("Contract", command.ContractId);

            DateOnly requestedOn = DateOnly.FromDateTime(now.UtcDateTime);
            var assessment = AssessForCreation(contract, requestedOn);

            var request = CancellationRequest.Create(
                contract.Id,
                contract.CustomerId,
                idempotencyKey,
                now,
                assessment);

            var stored = await cancellationRequests.TryCreateAsync(request, cancellationToken);

            CreateCancellationRequestResult result = stored.Outcome switch
            {
                CancellationRequestStoreOutcome.Created => ToResult(
                    stored.Request,
                    isReplay: false),
                CancellationRequestStoreOutcome.Replayed => ReplayResult(
                    stored.Request,
                    command.ContractId),
                CancellationRequestStoreOutcome.OpenRequestExists =>
                    throw new ApplicationConflictException(
                        "cancellation_request_already_open",
                        "A different cancellation request is already open for this contract."),
                CancellationRequestStoreOutcome.IdempotencyKeyConflict =>
                    throw new ApplicationConflictException(
                        "idempotency_key_conflict",
                        "The idempotency key has already been used for a different operation."),
                _ => throw new InvalidOperationException(
                    $"Unsupported store outcome '{stored.Outcome}'."),
            };

            RecordSuccess(activity, result, startedAt);
            return result;
        }
        catch (Exception exception)
        {
            string outcome = exception is OperationCanceledException
                ? "cancelled"
                : "failed";

            activity?.SetTag("contractiq.outcome", outcome);
            ContractIqTelemetry.MarkError(activity, exception);
            ContractIqTelemetry.RecordCancellationCommand(
                outcome,
                isReplay: false,
                Stopwatch.GetElapsedTime(startedAt));
            throw;
        }
    }

    private static void RecordSuccess(
        Activity? activity,
        CreateCancellationRequestResult result,
        long startedAt)
    {
        string outcome = result.IsReplay ? "replayed" : "created";

        activity?.SetTag("contractiq.outcome", outcome);
        activity?.SetTag("contractiq.command.is_replay", result.IsReplay);
        activity?.SetStatus(ActivityStatusCode.Ok);
        ContractIqTelemetry.RecordCancellationCommand(
            outcome,
            result.IsReplay,
            Stopwatch.GetElapsedTime(startedAt));
    }

    private static CancellationAssessment AssessForCreation(
        Contract contract,
        DateOnly requestedOn)
    {
        try
        {
            var assessment = contract.AssessCancellation(requestedOn);

            if (!assessment.IsAllowed)
            {
                throw new ApplicationConflictException(
                    "contract_not_cancellable",
                    $"The contract cannot be cancelled because its assessment is '{assessment.Reason}'.");
            }

            return assessment;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new ApplicationValidationException("requestedOn", exception.Message);
        }
    }

    private static string ValidateIdempotencyKey(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ApplicationValidationException(
                "idempotencyKey",
                "An idempotency key is required.");
        }

        string normalized = idempotencyKey.Trim();

        if (normalized.Length > 128)
        {
            throw new ApplicationValidationException(
                "idempotencyKey",
                "The idempotency key cannot exceed 128 characters.");
        }

        return normalized;
    }

    private static CreateCancellationRequestResult ReplayResult(
        CancellationRequest request,
        Guid contractId)
    {
        EnsureReplayMatchesContract(request, contractId);
        return ToResult(request, isReplay: true);
    }

    private static void EnsureReplayMatchesContract(CancellationRequest request, Guid contractId)
    {
        if (request.ContractId != contractId)
        {
            throw new ApplicationConflictException(
                "idempotency_key_conflict",
                "The idempotency key has already been used for a different operation.");
        }
    }

    private static CreateCancellationRequestResult ToResult(
        CancellationRequest request,
        bool isReplay) =>
        new(
            new CancellationRequestDto(
                request.Id,
                request.ContractId,
                request.CustomerId,
                request.CreatedAtUtc,
                request.RequestedOn,
                request.EarliestTerminationDate,
                MoneyDto.FromDomain(request.Penalty),
                request.Status),
            isReplay);
}
