using ContractIQ.Domain.Contracts;

namespace ContractIQ.Application.Abstractions.Persistence;

public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken);
}
