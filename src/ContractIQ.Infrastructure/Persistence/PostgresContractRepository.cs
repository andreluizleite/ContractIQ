using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Domain.Contracts;
using ContractIQ.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractIQ.Infrastructure.Persistence;

internal sealed class PostgresContractRepository(ContractIqDbContext dbContext)
    : IContractRepository
{
    public async Task<Contract?> GetByIdAsync(
        Guid contractId,
        CancellationToken cancellationToken)
    {
        ContractRecord? record = await dbContext.Contracts
            .AsNoTracking()
            .SingleOrDefaultAsync(contract => contract.Id == contractId, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    private static Contract ToDomain(ContractRecord record) =>
        new(
            record.Id,
            record.CustomerId,
            record.StartDate,
            new Money(record.MonthlyFeeAmount, record.MonthlyFeeCurrency),
            new TerminationTerms(
                record.NoticePeriodDays,
                record.MinimumCommitmentEndDate,
                record.EarlyTerminationPenaltyRate),
            (ContractStatus)record.Status);
}
