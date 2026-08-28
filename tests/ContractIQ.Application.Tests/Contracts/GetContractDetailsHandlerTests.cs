using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Common.Models;
using ContractIQ.Application.Contracts.GetContractDetails;
using ContractIQ.Domain.Contracts;
using Xunit;

namespace ContractIQ.Application.Tests.Contracts;

public sealed class GetContractDetailsHandlerTests
{
    [Fact]
    public async Task HandleAsync_maps_contract_details()
    {
        var contract = ApplicationTestData.CreateContract();
        var handler = new GetContractDetailsHandler(new FakeContractRepository(contract));

        ContractDetailsDto result = await handler.HandleAsync(
            new GetContractDetailsQuery(contract.Id),
            CancellationToken.None);

        Assert.Equal(contract.Id, result.Id);
        Assert.Equal(contract.CustomerId, result.CustomerId);
        Assert.Equal(contract.StartDate, result.StartDate);
        Assert.Equal(ContractStatus.Active, result.Status);
        Assert.Equal(new MoneyDto(1_000m, "BRL"), result.MonthlyFee);
        Assert.Equal(30, result.NoticePeriodDays);
        Assert.Equal(new DateOnly(2026, 12, 31), result.MinimumCommitmentEndDate);
        Assert.Equal(0.25m, result.EarlyTerminationPenaltyRate);
    }

    [Fact]
    public async Task HandleAsync_throws_when_contract_does_not_exist()
    {
        var contractId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var handler = new GetContractDetailsHandler(new FakeContractRepository());

        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => handler.HandleAsync(
                new GetContractDetailsQuery(contractId),
                CancellationToken.None));

        Assert.Equal("Contract", exception.ResourceName);
        Assert.Equal(contractId, exception.ResourceId);
    }
}
