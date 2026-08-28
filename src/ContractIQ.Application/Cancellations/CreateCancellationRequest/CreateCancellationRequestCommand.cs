namespace ContractIQ.Application.Cancellations.CreateCancellationRequest;

public sealed record CreateCancellationRequestCommand(Guid ContractId, string IdempotencyKey);
