using ContractIQ.Application.Common.Models;
using ContractIQ.Domain.Contracts;

namespace ContractIQ.Application.Contracts.ListCustomerContracts;

public sealed record ContractSummaryDto(
    Guid Id,
    Guid CustomerId,
    DateOnly StartDate,
    ContractStatus Status,
    MoneyDto MonthlyFee);
