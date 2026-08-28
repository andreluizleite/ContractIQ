using ContractIQ.Domain.Contracts;
using Xunit;

namespace ContractIQ.Domain.Tests.Contracts;

public sealed class ContractTests
{
    [Fact]
    public void Constructor_rejects_empty_contract_identifier()
    {
        Assert.Throws<ArgumentException>(() => ContractBuilder.Default.WithId(Guid.Empty).Build());
    }

    [Fact]
    public void Constructor_rejects_empty_customer_identifier()
    {
        Assert.Throws<ArgumentException>(
            () => ContractBuilder.Default.WithCustomerId(Guid.Empty).Build());
    }

    [Fact]
    public void Constructor_rejects_null_monthly_fee()
    {
        Assert.Throws<ArgumentNullException>(
            () => ContractBuilder.Default.WithMonthlyFee(null!).Build());
    }

    [Fact]
    public void Constructor_rejects_negative_monthly_fee()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ContractBuilder.Default.WithMonthlyFee(new Money(-0.01m, "BRL")).Build());
    }

    [Fact]
    public void Constructor_rejects_null_termination_terms()
    {
        Assert.Throws<ArgumentNullException>(
            () => ContractBuilder.Default.WithTerminationTerms(null!).Build());
    }

    [Fact]
    public void Constructor_rejects_commitment_ending_before_contract_start()
    {
        var terms = new TerminationTerms(30, new DateOnly(2025, 12, 31), 0.25m);

        Assert.Throws<ArgumentException>(
            () => ContractBuilder.Default.WithTerminationTerms(terms).Build());
    }

    [Fact]
    public void Constructor_rejects_undefined_status()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ContractBuilder.Default.WithStatus((ContractStatus)999).Build());
    }

    [Fact]
    public void AssessCancellation_rejects_request_before_contract_start()
    {
        var contract = ContractBuilder.Default.Build();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => contract.AssessCancellation(new DateOnly(2025, 12, 31)));
    }

    [Fact]
    public void AssessCancellation_rejects_null_time_provider()
    {
        var contract = ContractBuilder.Default.Build();

        Assert.Throws<ArgumentNullException>(() => contract.AssessCancellation(null!));
    }

    [Fact]
    public void Active_contract_before_commitment_end_has_penalty_for_each_monthly_period()
    {
        var terms = new TerminationTerms(
            noticePeriodDays: 0,
            minimumCommitmentEndDate: new DateOnly(2026, 3, 1),
            earlyTerminationPenaltyRate: 0.25m);
        var contract = ContractBuilder.Default
            .WithMonthlyFee(new Money(1_000m, "BRL"))
            .WithTerminationTerms(terms)
            .Build();

        CancellationAssessment assessment = contract.AssessCancellation(new DateOnly(2026, 1, 1));

        Assert.True(assessment.IsAllowed);
        Assert.Equal(CancellationAssessmentReason.Allowed, assessment.Reason);
        Assert.Equal(new DateOnly(2026, 1, 1), assessment.RequestedOn);
        Assert.Equal(new DateOnly(2026, 1, 1), assessment.EarliestTerminationDate);
        Assert.Equal(2, assessment.ChargeableMonthlyPeriods);
        Assert.Equal(new Money(500m, "BRL"), assessment.Penalty);
        Assert.True(assessment.HasPenalty);
    }

    [Fact]
    public void Active_contract_at_exact_commitment_boundary_has_no_penalty()
    {
        var terms = new TerminationTerms(
            noticePeriodDays: 30,
            minimumCommitmentEndDate: new DateOnly(2026, 3, 31),
            earlyTerminationPenaltyRate: 0.25m);
        var contract = ContractBuilder.Default.WithTerminationTerms(terms).Build();

        CancellationAssessment assessment = contract.AssessCancellation(new DateOnly(2026, 3, 1));

        Assert.True(assessment.IsAllowed);
        Assert.Equal(new DateOnly(2026, 3, 31), assessment.EarliestTerminationDate);
        Assert.Equal(0, assessment.ChargeableMonthlyPeriods);
        Assert.Equal(Money.Zero("BRL"), assessment.Penalty);
        Assert.False(assessment.HasPenalty);
    }

    [Fact]
    public void Partial_final_month_counts_as_a_full_chargeable_period()
    {
        var terms = new TerminationTerms(
            noticePeriodDays: 0,
            minimumCommitmentEndDate: new DateOnly(2026, 3, 1),
            earlyTerminationPenaltyRate: 0.1m);
        var contract = ContractBuilder.Default
            .WithMonthlyFee(new Money(100m, "USD"))
            .WithTerminationTerms(terms)
            .Build();

        CancellationAssessment assessment = contract.AssessCancellation(new DateOnly(2026, 1, 31));

        Assert.Equal(2, assessment.ChargeableMonthlyPeriods);
        Assert.Equal(new Money(20m, "USD"), assessment.Penalty);
    }

    [Fact]
    public void Zero_penalty_rate_keeps_chargeable_periods_but_produces_no_penalty()
    {
        var terms = new TerminationTerms(
            noticePeriodDays: 0,
            minimumCommitmentEndDate: new DateOnly(2026, 3, 1),
            earlyTerminationPenaltyRate: 0m);
        var contract = ContractBuilder.Default.WithTerminationTerms(terms).Build();

        CancellationAssessment assessment = contract.AssessCancellation(new DateOnly(2026, 1, 1));

        Assert.Equal(new DateOnly(2026, 1, 1), assessment.EarliestTerminationDate);
        Assert.Equal(2, assessment.ChargeableMonthlyPeriods);
        Assert.Equal(Money.Zero("BRL"), assessment.Penalty);
        Assert.False(assessment.HasPenalty);
    }

    [Theory]
    [InlineData(ContractStatus.Cancelled, CancellationAssessmentReason.ContractAlreadyCancelled)]
    [InlineData(ContractStatus.Expired, CancellationAssessmentReason.ContractExpired)]
    public void Inactive_contract_is_not_allowed_and_has_no_penalty(
        ContractStatus status,
        CancellationAssessmentReason expectedReason)
    {
        var contract = ContractBuilder.Default.WithStatus(status).Build();

        CancellationAssessment assessment = contract.AssessCancellation(new DateOnly(2026, 2, 1));

        Assert.False(assessment.IsAllowed);
        Assert.Equal(expectedReason, assessment.Reason);
        Assert.Equal(0, assessment.ChargeableMonthlyPeriods);
        Assert.Equal(Money.Zero("BRL"), assessment.Penalty);
        Assert.False(assessment.HasPenalty);
    }

    [Fact]
    public void Notice_period_crosses_month_boundary()
    {
        var terms = new TerminationTerms(1, new DateOnly(2027, 1, 1), 0.25m);
        var contract = ContractBuilder.Default.WithTerminationTerms(terms).Build();

        CancellationAssessment assessment = contract.AssessCancellation(new DateOnly(2026, 1, 31));

        Assert.Equal(new DateOnly(2026, 2, 1), assessment.EarliestTerminationDate);
    }

    [Fact]
    public void Notice_period_includes_leap_day()
    {
        var terms = new TerminationTerms(1, new DateOnly(2029, 1, 1), 0.25m);
        var contract = ContractBuilder.Default.WithTerminationTerms(terms).Build();

        CancellationAssessment assessment = contract.AssessCancellation(new DateOnly(2028, 2, 28));

        Assert.Equal(new DateOnly(2028, 2, 29), assessment.EarliestTerminationDate);
    }

    [Fact]
    public void TimeProvider_uses_UTC_date_even_when_local_date_is_previous_day()
    {
        var terms = new TerminationTerms(1, new DateOnly(2027, 1, 1), 0.25m);
        var contract = ContractBuilder.Default.WithTerminationTerms(terms).Build();
        var utcNow = new DateTimeOffset(2026, 3, 1, 0, 30, 0, TimeSpan.Zero);
        var utcMinusEleven = TimeZoneInfo.CreateCustomTimeZone(
            "UTC-11-test",
            TimeSpan.FromHours(-11),
            "UTC-11 test",
            "UTC-11 test");
        var timeProvider = new FrozenTimeProvider(utcNow, utcMinusEleven);

        CancellationAssessment assessment = contract.AssessCancellation(timeProvider);

        Assert.Equal(new DateOnly(2026, 3, 1), assessment.RequestedOn);
        Assert.Equal(new DateOnly(2026, 3, 2), assessment.EarliestTerminationDate);
    }

    private sealed class FrozenTimeProvider(
        DateTimeOffset utcNow,
        TimeZoneInfo localTimeZone) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => localTimeZone;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed record ContractBuilder(
        Guid Id,
        Guid CustomerId,
        DateOnly StartDate,
        Money MonthlyFee,
        TerminationTerms TerminationTerms,
        ContractStatus Status)
    {
        public static ContractBuilder Default => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            new DateOnly(2026, 1, 1),
            new Money(1_000m, "BRL"),
            new TerminationTerms(30, new DateOnly(2026, 12, 31), 0.25m),
            ContractStatus.Active);

        public ContractBuilder WithId(Guid id) => this with { Id = id };

        public ContractBuilder WithCustomerId(Guid customerId) => this with { CustomerId = customerId };

        public ContractBuilder WithMonthlyFee(Money monthlyFee) => this with { MonthlyFee = monthlyFee };

        public ContractBuilder WithTerminationTerms(TerminationTerms terminationTerms) =>
            this with { TerminationTerms = terminationTerms };

        public ContractBuilder WithStatus(ContractStatus status) => this with { Status = status };

        public Contract Build() => new(Id, CustomerId, StartDate, MonthlyFee, TerminationTerms, Status);
    }
}
