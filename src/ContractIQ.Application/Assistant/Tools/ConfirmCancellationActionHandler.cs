using System.Diagnostics;
using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Application.Cancellations.CreateCancellationRequest;
using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Common.Observability;

namespace ContractIQ.Application.Assistant.Tools;

public sealed class ConfirmCancellationActionHandler(
    IContractRepository contracts,
    CreateCancellationRequestHandler createCancellationRequest,
    IAssistantWriteTransaction transaction,
    IAssistantToolAudit audit,
    TimeProvider timeProvider)
{
    public async Task<CreateCancellationRequestResult> HandleAsync(
        ConfirmCancellationActionCommand command,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = ContractIqTelemetry.StartActivity(
            "contractiq.assistant.tool.execute");
        activity?.SetTag("gen_ai.tool.name", AssistantToolNames.CreateCancellation);
        activity?.SetTag("contractiq.tool.state_changing", true);

        ArgumentNullException.ThrowIfNull(command);

        if (!command.Confirmed)
        {
            await RecordAsync(command, "confirmation_missing", cancellationToken);
            activity?.SetTag("contractiq.outcome", "confirmation_missing");
            activity?.SetStatus(ActivityStatusCode.Error, "ConfirmationMissing");
            throw new ApplicationValidationException(
                nameof(command.Confirmed),
                "Explicit user confirmation is required before executing this tool.");
        }

        if (!string.Equals(
            command.Intent?.Trim(),
            AssistantToolNames.CreateCancellation,
            StringComparison.Ordinal))
        {
            await RecordAsync(command, "invalid_intent", cancellationToken);
            activity?.SetTag("contractiq.outcome", "invalid_intent");
            activity?.SetStatus(ActivityStatusCode.Error, "InvalidIntent");
            throw new ApplicationValidationException(
                nameof(command.Intent),
                $"Intent must be '{AssistantToolNames.CreateCancellation}'.");
        }

        try
        {
            CreateCancellationRequestResult result = await transaction.ExecuteAsync(
                async transactionCancellationToken =>
                {
                    var contract = await contracts.GetByIdAsync(
                        command.ContractId,
                        transactionCancellationToken);

                    if (contract is null || contract.CustomerId != command.CustomerId)
                    {
                        throw new ResourceNotFoundException("Contract", command.ContractId);
                    }

                    return await createCancellationRequest.HandleAsync(
                        new CreateCancellationRequestCommand(
                            command.ContractId,
                            command.IdempotencyKey),
                        transactionCancellationToken);
                },
                cancellationToken);

            await RecordAsync(
                command,
                result.IsReplay ? "replayed" : "created",
                cancellationToken);
            activity?.SetTag(
                "contractiq.outcome",
                result.IsReplay ? "replayed" : "created");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception exception)
        {
            string outcome = exception is OperationCanceledException
                ? "cancelled"
                : "rejected";

            activity?.SetTag(
                "contractiq.outcome",
                outcome);
            ContractIqTelemetry.MarkError(activity, exception);
            await RecordAsync(command, outcome, CancellationToken.None);
            throw;
        }
    }

    private Task RecordAsync(
        ConfirmCancellationActionCommand command,
        string outcome,
        CancellationToken cancellationToken) =>
        audit.RecordAsync(
            new AssistantToolAuditEvent(
                Guid.NewGuid(),
                AssistantToolNames.CreateCancellation,
                command.CustomerId,
                command.ContractId,
                outcome,
                StateChanging: true,
                timeProvider.GetUtcNow()),
            cancellationToken);
}
