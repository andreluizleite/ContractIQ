using ContractIQ.Domain.Contracts;

namespace ContractIQ.Domain.Cancellations;

public sealed class CancellationRequest
{
    private CancellationRequest(
        Guid id,
        Guid contractId,
        Guid customerId,
        string idempotencyKey,
        DateTimeOffset createdAtUtc,
        DateOnly requestedOn,
        DateOnly earliestTerminationDate,
        Money penalty,
        CancellationRequestStatus status)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A cancellation request identifier is required.", nameof(id));
        }

        if (contractId == Guid.Empty)
        {
            throw new ArgumentException("A contract identifier is required.", nameof(contractId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A customer identifier is required.", nameof(customerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentNullException.ThrowIfNull(penalty);

        if (earliestTerminationDate < requestedOn)
        {
            throw new ArgumentException(
                "The earliest termination date cannot precede the request date.",
                nameof(earliestTerminationDate));
        }

        if (penalty.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(penalty), "The penalty cannot be negative.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Id = id;
        ContractId = contractId;
        CustomerId = customerId;
        IdempotencyKey = idempotencyKey.Trim();
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        RequestedOn = requestedOn;
        EarliestTerminationDate = earliestTerminationDate;
        Penalty = penalty;
        Status = status;
    }

    public Guid Id { get; }

    public Guid ContractId { get; }

    public Guid CustomerId { get; }

    public string IdempotencyKey { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateOnly RequestedOn { get; }

    public DateOnly EarliestTerminationDate { get; }

    public Money Penalty { get; }

    public CancellationRequestStatus Status { get; }

    public bool IsOpen => Status == CancellationRequestStatus.PendingReview;

    public static CancellationRequest Create(
        Guid contractId,
        Guid customerId,
        string idempotencyKey,
        DateTimeOffset createdAtUtc,
        CancellationAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        if (!assessment.IsAllowed)
        {
            throw new InvalidOperationException("A cancellation request requires an allowed assessment.");
        }

        return new CancellationRequest(
            Guid.NewGuid(),
            contractId,
            customerId,
            idempotencyKey,
            createdAtUtc,
            assessment.RequestedOn,
            assessment.EarliestTerminationDate,
            assessment.Penalty,
            CancellationRequestStatus.PendingReview);
    }

    /// <summary>
    /// Reconstructs a previously persisted request without generating new identity or business state.
    /// Persistence adapters should use this method instead of the creation factory.
    /// </summary>
    public static CancellationRequest Rehydrate(
        Guid id,
        Guid contractId,
        Guid customerId,
        string idempotencyKey,
        DateTimeOffset createdAtUtc,
        DateOnly requestedOn,
        DateOnly earliestTerminationDate,
        Money penalty,
        CancellationRequestStatus status) =>
        new(
            id,
            contractId,
            customerId,
            idempotencyKey,
            createdAtUtc,
            requestedOn,
            earliestTerminationDate,
            penalty,
            status);
}
