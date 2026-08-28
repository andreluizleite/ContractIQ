using ContractIQ.Domain.Customers;

namespace ContractIQ.Application.Abstractions.Persistence;

public interface ICustomerRepository
{
    Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken);
}
