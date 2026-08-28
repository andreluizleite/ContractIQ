using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Application.Common.Models;

namespace ContractIQ.Application.Contracts.ListCustomerContracts;

public sealed class ListCustomerContractsHandler(IContractRepository contracts)
{
    public async Task<IReadOnlyList<ContractSummaryDto>> HandleAsync(
        ListCustomerContractsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var customerContracts = await contracts.ListByCustomerIdAsync(
            query.CustomerId,
            cancellationToken);

        return customerContracts
            .Select(contract => new ContractSummaryDto(
                contract.Id,
                contract.CustomerId,
                contract.StartDate,
                contract.Status,
                MoneyDto.FromDomain(contract.MonthlyFee)))
            .ToArray();
    }
}
