using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Common.Models;

namespace ContractIQ.Application.Contracts.AssessCancellation;

public sealed class AssessCancellationHandler(
    IContractRepository contracts,
    TimeProvider timeProvider)
{
    public async Task<CancellationAssessmentDto> HandleAsync(
        AssessCancellationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var contract = await contracts.GetByIdAsync(query.ContractId, cancellationToken)
            ?? throw new ResourceNotFoundException("Contract", query.ContractId);

        var assessment = contract.AssessCancellation(timeProvider);

        return new CancellationAssessmentDto(
            contract.Id,
            assessment.IsAllowed,
            assessment.Reason,
            assessment.RequestedOn,
            assessment.EarliestTerminationDate,
            assessment.ChargeableMonthlyPeriods,
            MoneyDto.FromDomain(assessment.Penalty),
            assessment.HasPenalty);
    }
}
