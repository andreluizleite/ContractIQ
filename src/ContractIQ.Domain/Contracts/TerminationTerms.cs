namespace ContractIQ.Domain.Contracts;

public sealed record TerminationTerms
{
    public TerminationTerms(
        int noticePeriodDays,
        DateOnly minimumCommitmentEndDate,
        decimal earlyTerminationPenaltyRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(noticePeriodDays);

        if (earlyTerminationPenaltyRate is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(earlyTerminationPenaltyRate),
                "The early termination penalty rate must be between zero and one.");
        }

        NoticePeriodDays = noticePeriodDays;
        MinimumCommitmentEndDate = minimumCommitmentEndDate;
        EarlyTerminationPenaltyRate = earlyTerminationPenaltyRate;
    }

    public int NoticePeriodDays { get; }

    /// <summary>
    /// The first date on which termination no longer incurs an early termination penalty.
    /// </summary>
    public DateOnly MinimumCommitmentEndDate { get; }

    /// <summary>
    /// A decimal rate where 0.25 represents twenty-five percent of each chargeable monthly fee.
    /// </summary>
    public decimal EarlyTerminationPenaltyRate { get; }
}
