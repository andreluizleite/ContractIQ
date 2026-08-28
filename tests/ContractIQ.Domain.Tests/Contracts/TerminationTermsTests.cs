using ContractIQ.Domain.Contracts;
using Xunit;

namespace ContractIQ.Domain.Tests.Contracts;

public sealed class TerminationTermsTests
{
    [Fact]
    public void Constructor_accepts_zero_notice_and_boundary_rates()
    {
        var noPenalty = new TerminationTerms(0, new DateOnly(2026, 12, 31), 0m);
        var fullPenalty = new TerminationTerms(0, new DateOnly(2026, 12, 31), 1m);

        Assert.Equal(0, noPenalty.NoticePeriodDays);
        Assert.Equal(0m, noPenalty.EarlyTerminationPenaltyRate);
        Assert.Equal(1m, fullPenalty.EarlyTerminationPenaltyRate);
    }

    [Fact]
    public void Constructor_rejects_negative_notice_period()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TerminationTerms(-1, new DateOnly(2026, 12, 31), 0.25m));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Constructor_rejects_penalty_rate_outside_zero_to_one(decimal rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TerminationTerms(30, new DateOnly(2026, 12, 31), rate));
    }
}
