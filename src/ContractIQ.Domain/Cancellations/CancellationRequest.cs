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
        CancellationAssessment assessment)
    {
        Id = id;
        ContractId = contractId;
        CustomerId = customerId;
        IdempotencyKey = idempotencyKey;
        CreatedAtUtc = createdAtUtc;
        RequestedOn = assessment.RequestedOn;
        EarliestTerminationDate = assessment.EarliestTerminationDate;
        Penalty = assessment.Penalty;
        Status = CancellationRequestStatus.PendingReview;
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
        if (contractId == Guid.Empty)
        {
            throw new ArgumentException("A contract identifier is required.", nameof(contractId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A customer identifier is required.", nameof(customerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentNullException.ThrowIfNull(assessment);

        if (!assessment.IsAllowed)
        {
            throw new InvalidOperationException("A cancellation request requires an allowed assessment.");
        }

        return new CancellationRequest(
            Guid.NewGuid(),
            contractId,
            customerId,
            idempotencyKey.Trim(),
            createdAtUtc.ToUniversalTime(),
            assessment);
    }
}
