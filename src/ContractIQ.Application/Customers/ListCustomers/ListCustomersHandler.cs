using ContractIQ.Application.Abstractions.Persistence;

namespace ContractIQ.Application.Customers.ListCustomers;

public sealed class ListCustomersHandler(ICustomerRepository customers)
{
    public async Task<IReadOnlyList<CustomerSummaryDto>> HandleAsync(
        ListCustomersQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var results = await customers.ListAsync(cancellationToken);

        return results
            .Select(customer => new CustomerSummaryDto(customer.Id, customer.Name))
            .ToArray();
    }
}
