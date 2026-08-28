using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Domain.Cancellations;
using ContractIQ.Domain.Contracts;
using ContractIQ.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ContractIQ.Infrastructure.Persistence;

internal sealed class PostgresCancellationRequestStore(ContractIqDbContext dbContext)
    : ICancellationRequestStore
{
    public async Task<CancellationRequest?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        CancellationRequestRecord? record = await dbContext.CancellationRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                request => request.IdempotencyKey == idempotencyKey,
                cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    public async Task<CancellationRequestStoreResult> TryCreateAsync(
        CancellationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        CancellationRequestStoreResult? existing = await FindConflictAsync(
            request,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var record = ToRecord(request);
        dbContext.CancellationRequests.Add(record);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            return new CancellationRequestStoreResult(
                CancellationRequestStoreOutcome.Created,
                request);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            dbContext.Entry(record).State = EntityState.Detached;

            CancellationRequestStoreResult? concurrentConflict = await FindConflictAsync(
                request,
                cancellationToken);

            if (concurrentConflict is not null)
            {
                return concurrentConflict;
            }

            throw;
        }
    }

    private async Task<CancellationRequestStoreResult?> FindConflictAsync(
        CancellationRequest candidate,
        CancellationToken cancellationToken)
    {
        CancellationRequestRecord? sameKey = await dbContext.CancellationRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                request => request.IdempotencyKey == candidate.IdempotencyKey,
                cancellationToken);

        if (sameKey is not null)
        {
            CancellationRequest existing = ToDomain(sameKey);
            CancellationRequestStoreOutcome outcome = existing.ContractId == candidate.ContractId
                ? CancellationRequestStoreOutcome.Replayed
                : CancellationRequestStoreOutcome.IdempotencyKeyConflict;

            return new CancellationRequestStoreResult(outcome, existing);
        }

        int openStatus = (int)CancellationRequestStatus.PendingReview;
        CancellationRequestRecord? openRequest = await dbContext.CancellationRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                request => request.ContractId == candidate.ContractId && request.Status == openStatus,
                cancellationToken);

        return openRequest is null
            ? null
            : new CancellationRequestStoreResult(
                CancellationRequestStoreOutcome.OpenRequestExists,
                ToDomain(openRequest));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        };

    private static CancellationRequestRecord ToRecord(CancellationRequest request) =>
        new()
        {
            Id = request.Id,
            ContractId = request.ContractId,
            CustomerId = request.CustomerId,
            IdempotencyKey = request.IdempotencyKey,
            CreatedAtUtc = request.CreatedAtUtc,
            RequestedOn = request.RequestedOn,
            EarliestTerminationDate = request.EarliestTerminationDate,
            PenaltyAmount = request.Penalty.Amount,
            PenaltyCurrency = request.Penalty.Currency,
            Status = (int)request.Status,
        };

    private static CancellationRequest ToDomain(CancellationRequestRecord record) =>
        CancellationRequest.Rehydrate(
            record.Id,
            record.ContractId,
            record.CustomerId,
            record.IdempotencyKey,
            record.CreatedAtUtc,
            record.RequestedOn,
            record.EarliestTerminationDate,
            new Money(record.PenaltyAmount, record.PenaltyCurrency),
            (CancellationRequestStatus)record.Status);
}
