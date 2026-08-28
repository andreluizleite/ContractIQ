using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Common.Models;

namespace ContractIQ.Application.Contracts.GetContractDetails;

public sealed class GetContractDetailsHandler(IContractRepository contracts)
{
    public async Task<ContractDetailsDto> HandleAsync(
        GetContractDetailsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var contract = await contracts.GetByIdAsync(query.ContractId, cancellationToken)
            ?? throw new ResourceNotFoundException("Contract", query.ContractId);

        return new ContractDetailsDto(
            contract.Id,
            contract.CustomerId,
            contract.StartDate,
            contract.Status,
            MoneyDto.FromDomain(contract.MonthlyFee),
            contract.TerminationTerms.NoticePeriodDays,
            contract.TerminationTerms.MinimumCommitmentEndDate,
            contract.TerminationTerms.EarlyTerminationPenaltyRate);
    }
}
