namespace ContractIQ.Domain.Contracts;

public sealed record CancellationAssessment
{
    internal CancellationAssessment(
        bool isAllowed,
        CancellationAssessmentReason reason,
        DateOnly requestedOn,
        DateOnly earliestTerminationDate,
        int chargeableMonthlyPeriods,
        Money penalty)
    {
        IsAllowed = isAllowed;
        Reason = reason;
        RequestedOn = requestedOn;
        EarliestTerminationDate = earliestTerminationDate;
        ChargeableMonthlyPeriods = chargeableMonthlyPeriods;
        Penalty = penalty;
    }

    public bool IsAllowed { get; }

    public CancellationAssessmentReason Reason { get; }

    public DateOnly RequestedOn { get; }

    public DateOnly EarliestTerminationDate { get; }

    public int ChargeableMonthlyPeriods { get; }

    public Money Penalty { get; }

    public bool HasPenalty => Penalty.Amount > 0m;
}

public enum CancellationAssessmentReason
{
    Allowed = 1,
    ContractAlreadyCancelled = 2,
    ContractExpired = 3,
}
