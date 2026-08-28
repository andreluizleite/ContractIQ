namespace ContractIQ.Domain.Contracts;

public sealed class Contract
{
    public Contract(
        Guid id,
        Guid customerId,
        DateOnly startDate,
        Money monthlyFee,
        TerminationTerms terminationTerms,
        ContractStatus status = ContractStatus.Active)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A contract identifier is required.", nameof(id));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A customer identifier is required.", nameof(customerId));
        }

        ArgumentNullException.ThrowIfNull(monthlyFee);
        ArgumentNullException.ThrowIfNull(terminationTerms);

        if (monthlyFee.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyFee), "The monthly fee cannot be negative.");
        }

        if (terminationTerms.MinimumCommitmentEndDate < startDate)
        {
            throw new ArgumentException(
                "The minimum commitment cannot end before the contract starts.",
                nameof(terminationTerms));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Id = id;
        CustomerId = customerId;
        StartDate = startDate;
        MonthlyFee = monthlyFee;
        TerminationTerms = terminationTerms;
        Status = status;
    }

    public Guid Id { get; }

    public Guid CustomerId { get; }

    public DateOnly StartDate { get; }

    public Money MonthlyFee { get; }

    public TerminationTerms TerminationTerms { get; }

    public ContractStatus Status { get; }

    public CancellationAssessment AssessCancellation(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateOnly requestedOn = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        return AssessCancellation(requestedOn);
    }

    public CancellationAssessment AssessCancellation(DateOnly requestedOn)
    {
        if (requestedOn < StartDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedOn),
                "A cancellation cannot be requested before the contract starts.");
        }

        DateOnly earliestTerminationDate = requestedOn.AddDays(TerminationTerms.NoticePeriodDays);

        if (Status == ContractStatus.Cancelled)
        {
            return NotAllowed(
                CancellationAssessmentReason.ContractAlreadyCancelled,
                requestedOn,
                earliestTerminationDate);
        }

        if (Status == ContractStatus.Expired)
        {
            return NotAllowed(
                CancellationAssessmentReason.ContractExpired,
                requestedOn,
                earliestTerminationDate);
        }

        int chargeableMonthlyPeriods = CountChargeableMonthlyPeriods(earliestTerminationDate);
        Money penalty = MonthlyFee.Multiply(
            TerminationTerms.EarlyTerminationPenaltyRate * chargeableMonthlyPeriods);

        return new CancellationAssessment(
            isAllowed: true,
            CancellationAssessmentReason.Allowed,
            requestedOn,
            earliestTerminationDate,
            chargeableMonthlyPeriods,
            penalty);
    }

    private CancellationAssessment NotAllowed(
        CancellationAssessmentReason reason,
        DateOnly requestedOn,
        DateOnly earliestTerminationDate) =>
        new(
            isAllowed: false,
            reason,
            requestedOn,
            earliestTerminationDate,
            chargeableMonthlyPeriods: 0,
            Money.Zero(MonthlyFee.Currency));

    private int CountChargeableMonthlyPeriods(DateOnly earliestTerminationDate)
    {
        int periods = 0;
        DateOnly periodStart = earliestTerminationDate;

        // Every monthly period that starts before commitment ends is charged; a final partial period counts.
        while (periodStart < TerminationTerms.MinimumCommitmentEndDate)
        {
            periods++;
            periodStart = periodStart.AddMonths(1);
        }

        return periods;
    }
}
