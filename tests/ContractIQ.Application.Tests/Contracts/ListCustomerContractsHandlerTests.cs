using ContractIQ.Application.Contracts.ListCustomerContracts;
using Xunit;

namespace ContractIQ.Application.Tests.Contracts;

public sealed class ListCustomerContractsHandlerTests
{
    [Fact]
    public async Task Handle_returns_only_the_customer_contracts()
    {
        var acmeContract = ApplicationTestData.CreateContract();
        var anotherCustomerContract = ApplicationTestData.CreateContract(
            id: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            customerId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var handler = new ListCustomerContractsHandler(
            new FakeContractRepository(acmeContract, anotherCustomerContract));

        var result = await handler.HandleAsync(
            new ListCustomerContractsQuery(ApplicationTestData.AcmeCustomerId),
            CancellationToken.None);

        var contract = Assert.Single(result);
        Assert.Equal(acmeContract.Id, contract.Id);
        Assert.Equal(acmeContract.CustomerId, contract.CustomerId);
        Assert.Equal(acmeContract.MonthlyFee.Amount, contract.MonthlyFee.Amount);
    }

    [Fact]
    public async Task Handle_returns_empty_when_customer_has_no_contracts()
    {
        var handler = new ListCustomerContractsHandler(new FakeContractRepository());

        var result = await handler.HandleAsync(
            new ListCustomerContractsQuery(ApplicationTestData.AcmeCustomerId),
            CancellationToken.None);

        Assert.Empty(result);
    }
}
