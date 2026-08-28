using ContractIQ.Domain.Contracts;
using Xunit;

namespace ContractIQ.Domain.Tests.Contracts;

/// <summary>
/// Demonstrates the business flow with the same kind of question the assistant will answer later.
/// The expected outcome still comes entirely from deterministic domain code.
/// </summary>
public sealed class ContractCancellationScenarioTests
{
    [Fact]
    public void Acme_can_cancel_now_with_an_early_termination_penalty()
    {
        var contract = CreateAcmeContract();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero));

        CancellationAssessment assessment = contract.AssessCancellation(timeProvider);

        Assert.True(assessment.IsAllowed);
        Assert.Equal(CancellationAssessmentReason.Allowed, assessment.Reason);
        Assert.Equal(new DateOnly(2026, 9, 1), assessment.RequestedOn);
        Assert.Equal(new DateOnly(2026, 10, 1), assessment.EarliestTerminationDate);
        Assert.Equal(3, assessment.ChargeableMonthlyPeriods);
        Assert.Equal(new Money(750m, "USD"), assessment.Penalty);
    }

    [Fact]
    public void Acme_can_cancel_without_a_penalty_when_notice_reaches_commitment_end()
    {
        var contract = CreateAcmeContract();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero));

        CancellationAssessment assessment = contract.AssessCancellation(timeProvider);

        Assert.True(assessment.IsAllowed);
        Assert.Equal(new DateOnly(2026, 12, 31), assessment.EarliestTerminationDate);
        Assert.Equal(0, assessment.ChargeableMonthlyPeriods);
        Assert.Equal(Money.Zero("USD"), assessment.Penalty);
        Assert.False(assessment.HasPenalty);
    }

    private static Contract CreateAcmeContract() =>
        new(
            id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            customerId: Guid.Parse("acacacac-acac-acac-acac-acacacacacac"),
            startDate: new DateOnly(2026, 1, 1),
            monthlyFee: new Money(1_000m, "USD"),
            terminationTerms: new TerminationTerms(
                noticePeriodDays: 30,
                minimumCommitmentEndDate: new DateOnly(2026, 12, 31),
                earlyTerminationPenaltyRate: 0.25m));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
