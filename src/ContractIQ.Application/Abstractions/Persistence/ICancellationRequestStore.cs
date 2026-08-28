using ContractIQ.Domain.Cancellations;

namespace ContractIQ.Application.Abstractions.Persistence;

public interface ICancellationRequestStore
{
    Task<CancellationRequest?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically enforces unique idempotency keys and at most one open request per contract.
    /// </summary>
    Task<CancellationRequestStoreResult> TryCreateAsync(
        CancellationRequest request,
        CancellationToken cancellationToken);
}

public sealed record CancellationRequestStoreResult(
    CancellationRequestStoreOutcome Outcome,
    CancellationRequest Request);

public enum CancellationRequestStoreOutcome
{
    Created = 1,
    Replayed = 2,
    OpenRequestExists = 3,
    IdempotencyKeyConflict = 4,
}
