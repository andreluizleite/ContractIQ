namespace ContractIQ.Application.Assistant.Tools;

public sealed record ConfirmCancellationActionCommand(
    Guid CustomerId,
    Guid ContractId,
    string Intent,
    bool Confirmed,
    string IdempotencyKey);
