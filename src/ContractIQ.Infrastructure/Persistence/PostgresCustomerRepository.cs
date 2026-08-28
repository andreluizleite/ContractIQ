using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace ContractIQ.Infrastructure.Persistence;

internal sealed class PostgresCustomerRepository(ContractIqDbContext dbContext)
    : ICustomerRepository
{
    public async Task<IReadOnlyList<Customer>> ListAsync(
        CancellationToken cancellationToken)
    {
        var records = await dbContext.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.Name)
            .ThenBy(customer => customer.Id)
            .ToListAsync(cancellationToken);

        return records
            .Select(record => new Customer(record.Id, record.Name))
            .ToArray();
    }
}
