using ContractIQ.Application.Customers.ListCustomers;
using ContractIQ.Domain.Customers;
using Xunit;

namespace ContractIQ.Application.Tests.Customers;

public sealed class ListCustomersHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_customer_summaries_in_repository_order()
    {
        var acme = new Customer(ApplicationTestData.AcmeCustomerId, "ACME Corporation");
        var globex = new Customer(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "Globex Corporation");
        var handler = new ListCustomersHandler(new FakeCustomerRepository(acme, globex));

        var results = await handler.HandleAsync(new ListCustomersQuery(), CancellationToken.None);

        Assert.Collection(
            results,
            customer => Assert.Equal(
                new CustomerSummaryDto(acme.Id, acme.Name),
                customer),
            customer => Assert.Equal(
                new CustomerSummaryDto(globex.Id, globex.Name),
                customer));
    }
}
