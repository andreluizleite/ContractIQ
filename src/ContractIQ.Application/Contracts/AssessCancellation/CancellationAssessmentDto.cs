using ContractIQ.Application.Common.Models;
using ContractIQ.Domain.Contracts;

namespace ContractIQ.Application.Contracts.AssessCancellation;

public sealed record CancellationAssessmentDto(
    Guid ContractId,
    bool IsAllowed,
    CancellationAssessmentReason Reason,
    DateOnly RequestedOn,
    DateOnly EarliestTerminationDate,
    int ChargeableMonthlyPeriods,
    MoneyDto Penalty,
    bool HasPenalty);
