namespace ContractIQ.Application.Cancellations.CreateCancellationRequest;

public sealed record CreateCancellationRequestResult(
    CancellationRequestDto Request,
    bool IsReplay);
