using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Common.Models;
using ContractIQ.Application.Contracts.AssessCancellation;
using ContractIQ.Domain.Contracts;
using Xunit;

namespace ContractIQ.Application.Tests.Contracts;

public sealed class AssessCancellationHandlerTests
{
    [Fact]
    public async Task HandleAsync_uses_the_deterministic_UTC_date()
    {
        var contract = ApplicationTestData.CreateContract();
        var utcMinusEleven = TimeZoneInfo.CreateCustomTimeZone(
            "UTC-11-application-test",
            TimeSpan.FromHours(-11),
            "UTC-11 application test",
            "UTC-11 application test");
        var timeProvider = new MutableTimeProvider(
            new DateTimeOffset(2026, 3, 1, 0, 30, 0, TimeSpan.Zero),
            utcMinusEleven);
        var handler = new AssessCancellationHandler(
            new FakeContractRepository(contract),
            timeProvider);

        CancellationAssessmentDto result = await handler.HandleAsync(
            new AssessCancellationQuery(contract.Id),
            CancellationToken.None);

        Assert.Equal(contract.Id, result.ContractId);
        Assert.True(result.IsAllowed);
        Assert.Equal(CancellationAssessmentReason.Allowed, result.Reason);
        Assert.Equal(new DateOnly(2026, 3, 1), result.RequestedOn);
        Assert.Equal(new DateOnly(2026, 3, 31), result.EarliestTerminationDate);
        Assert.Equal(10, result.ChargeableMonthlyPeriods);
        Assert.Equal(new MoneyDto(2_500m, "BRL"), result.Penalty);
        Assert.True(result.HasPenalty);
    }

    [Fact]
    public async Task HandleAsync_throws_when_contract_does_not_exist()
    {
        var contractId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var handler = new AssessCancellationHandler(
            new FakeContractRepository(),
            new MutableTimeProvider(DateTimeOffset.Parse("2026-03-01T00:00:00Z")));

        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => handler.HandleAsync(
                new AssessCancellationQuery(contractId),
                CancellationToken.None));

        Assert.Equal(contractId, exception.ResourceId);
    }
}
