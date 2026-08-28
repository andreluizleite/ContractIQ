using ContractIQ.Application.Common.Models;
using ContractIQ.Domain.Cancellations;

namespace ContractIQ.Application.Cancellations.CreateCancellationRequest;

public sealed record CancellationRequestDto(
    Guid Id,
    Guid ContractId,
    Guid CustomerId,
    DateTimeOffset CreatedAtUtc,
    DateOnly RequestedOn,
    DateOnly EarliestTerminationDate,
    MoneyDto Penalty,
    CancellationRequestStatus Status);
