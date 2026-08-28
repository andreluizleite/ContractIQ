using ContractIQ.Application.Common.Models;
using ContractIQ.Domain.Contracts;

namespace ContractIQ.Application.Contracts.GetContractDetails;

public sealed record ContractDetailsDto(
    Guid Id,
    Guid CustomerId,
    DateOnly StartDate,
    ContractStatus Status,
    MoneyDto MonthlyFee,
    int NoticePeriodDays,
    DateOnly MinimumCommitmentEndDate,
    decimal EarlyTerminationPenaltyRate);
