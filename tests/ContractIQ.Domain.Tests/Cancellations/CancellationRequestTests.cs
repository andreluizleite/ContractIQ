using ContractIQ.Domain.Cancellations;
using ContractIQ.Domain.Contracts;
using Xunit;

namespace ContractIQ.Domain.Tests.Cancellations;

public sealed class CancellationRequestTests
{
    [Fact]
    public void Rehydrate_preserves_persisted_identity_snapshot_and_status()
    {
        Guid requestId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        Guid contractId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        Guid customerId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var createdAt = new DateTimeOffset(2026, 8, 28, 14, 30, 0, TimeSpan.FromHours(-3));
        var penalty = new Money(900m, "USD");

        CancellationRequest request = CancellationRequest.Rehydrate(
            requestId,
            contractId,
            customerId,
            " persisted-operation ",
            createdAt,
            new DateOnly(2026, 8, 28),
            new DateOnly(2026, 9, 27),
            penalty,
            CancellationRequestStatus.Approved);

        Assert.Equal(requestId, request.Id);
        Assert.Equal(contractId, request.ContractId);
        Assert.Equal(customerId, request.CustomerId);
        Assert.Equal("persisted-operation", request.IdempotencyKey);
        Assert.Equal(createdAt.ToUniversalTime(), request.CreatedAtUtc);
        Assert.Equal(new DateOnly(2026, 8, 28), request.RequestedOn);
        Assert.Equal(new DateOnly(2026, 9, 27), request.EarliestTerminationDate);
        Assert.Equal(penalty, request.Penalty);
        Assert.Equal(CancellationRequestStatus.Approved, request.Status);
        Assert.False(request.IsOpen);
    }

    [Fact]
    public void Rehydrate_rejects_termination_date_before_request_date()
    {
        Assert.Throws<ArgumentException>(() => CancellationRequest.Rehydrate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "operation-key",
            DateTimeOffset.UtcNow,
            new DateOnly(2026, 8, 28),
            new DateOnly(2026, 8, 27),
            Money.Zero("USD"),
            CancellationRequestStatus.PendingReview));
    }

    [Fact]
    public void Rehydrate_rejects_undefined_status()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CancellationRequest.Rehydrate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "operation-key",
            DateTimeOffset.UtcNow,
            new DateOnly(2026, 8, 28),
            new DateOnly(2026, 9, 27),
            Money.Zero("USD"),
            (CancellationRequestStatus)999));
    }
}
