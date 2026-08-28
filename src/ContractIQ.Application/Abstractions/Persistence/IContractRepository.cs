using ContractIQ.Domain.Contracts;

namespace ContractIQ.Application.Abstractions.Persistence;

public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Contract>> ListByCustomerIdAsync(
        Guid customerId,
        CancellationToken cancellationToken);
}
