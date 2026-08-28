using ContractIQ.Application.Cancellations.CreateCancellationRequest;
using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Common.Models;
using ContractIQ.Domain.Cancellations;
using ContractIQ.Domain.Contracts;
using Xunit;

namespace ContractIQ.Application.Tests.Cancellations;

public sealed class CreateCancellationRequestHandlerTests
{
    private static readonly DateTimeOffset FrozenUtc =
        DateTimeOffset.Parse("2026-03-01T00:30:00Z");

    [Fact]
    public async Task HandleAsync_creates_a_pending_review_snapshot()
    {
        var contract = ApplicationTestData.CreateContract();
        var store = new FakeCancellationRequestStore();
        var handler = CreateHandler(contract, store, new MutableTimeProvider(FrozenUtc));

        CreateCancellationRequestResult result = await handler.HandleAsync(
            new CreateCancellationRequestCommand(contract.Id, " request-001 "),
            CancellationToken.None);

        Assert.False(result.IsReplay);
        Assert.NotEqual(Guid.Empty, result.Request.Id);
        Assert.Equal(contract.Id, result.Request.ContractId);
        Assert.Equal(contract.CustomerId, result.Request.CustomerId);
        Assert.Equal(FrozenUtc, result.Request.CreatedAtUtc);
        Assert.Equal(new DateOnly(2026, 3, 1), result.Request.RequestedOn);
        Assert.Equal(new DateOnly(2026, 3, 31), result.Request.EarliestTerminationDate);
        Assert.Equal(new MoneyDto(2_500m, "BRL"), result.Request.Penalty);
        Assert.Equal(CancellationRequestStatus.PendingReview, result.Request.Status);

        var stored = Assert.Single(store.Requests);
        Assert.Equal("request-001", stored.IdempotencyKey);
        Assert.Equal(result.Request.Id, stored.Id);
    }

    [Fact]
    public async Task HandleAsync_replays_the_original_snapshot_after_time_advances()
    {
        var contract = ApplicationTestData.CreateContract();
        var store = new FakeCancellationRequestStore();
        var timeProvider = new MutableTimeProvider(FrozenUtc);
        var repository = new FakeContractRepository(contract);
        var handler = new CreateCancellationRequestHandler(repository, store, timeProvider);
        var command = new CreateCancellationRequestCommand(contract.Id, "request-001");

        CreateCancellationRequestResult created = await handler.HandleAsync(command, CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromDays(90));
        CreateCancellationRequestResult replay = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(replay.IsReplay);
        Assert.Equal(created.Request, replay.Request);
        Assert.Single(store.Requests);
        Assert.Equal(1, store.TryCreateCallCount);
        Assert.Equal(1, repository.CallCount);
    }

    [Fact]
    public async Task HandleAsync_rejects_same_key_for_a_different_contract()
    {
        var acmeContract = ApplicationTestData.CreateContract();
        var globexContract = ApplicationTestData.CreateContract(
            id: ApplicationTestData.GlobexContractId,
            customerId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var store = new FakeCancellationRequestStore();
        var repository = new FakeContractRepository(acmeContract, globexContract);
        var handler = new CreateCancellationRequestHandler(
            repository,
            store,
            new MutableTimeProvider(FrozenUtc));

        await handler.HandleAsync(
            new CreateCancellationRequestCommand(acmeContract.Id, "shared-key"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ApplicationConflictException>(
            () => handler.HandleAsync(
                new CreateCancellationRequestCommand(globexContract.Id, "shared-key"),
                CancellationToken.None));

        Assert.Equal("idempotency_key_conflict", exception.Code);
        Assert.Single(store.Requests);
        Assert.Equal(1, store.TryCreateCallCount);
        Assert.Equal(1, repository.CallCount);
    }

    [Fact]
    public async Task HandleAsync_rejects_a_different_key_when_an_open_request_exists()
    {
        var contract = ApplicationTestData.CreateContract();
        var store = new FakeCancellationRequestStore();
        var handler = CreateHandler(contract, store, new MutableTimeProvider(FrozenUtc));

        await handler.HandleAsync(
            new CreateCancellationRequestCommand(contract.Id, "request-001"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ApplicationConflictException>(
            () => handler.HandleAsync(
                new CreateCancellationRequestCommand(contract.Id, "request-002"),
                CancellationToken.None));

        Assert.Equal("cancellation_request_already_open", exception.Code);
        Assert.Single(store.Requests);
        Assert.Equal(2, store.TryCreateCallCount);
    }

    [Theory]
    [InlineData(ContractStatus.Cancelled)]
    [InlineData(ContractStatus.Expired)]
    public async Task HandleAsync_rejects_inactive_contract(ContractStatus status)
    {
        var contract = ApplicationTestData.CreateContract(status: status);
        var store = new FakeCancellationRequestStore();
        var handler = CreateHandler(contract, store, new MutableTimeProvider(FrozenUtc));

        var exception = await Assert.ThrowsAsync<ApplicationConflictException>(
            () => handler.HandleAsync(
                new CreateCancellationRequestCommand(contract.Id, "request-001"),
                CancellationToken.None));

        Assert.Equal("contract_not_cancellable", exception.Code);
        Assert.Empty(store.Requests);
        Assert.Equal(0, store.TryCreateCallCount);
    }

    [Fact]
    public async Task HandleAsync_throws_when_contract_does_not_exist()
    {
        var contractId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var store = new FakeCancellationRequestStore();
        var handler = new CreateCancellationRequestHandler(
            new FakeContractRepository(),
            store,
            new MutableTimeProvider(FrozenUtc));

        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => handler.HandleAsync(
                new CreateCancellationRequestCommand(contractId, "request-001"),
                CancellationToken.None));

        Assert.Equal(contractId, exception.ResourceId);
        Assert.Empty(store.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_rejects_missing_idempotency_key(string? key)
    {
        var contract = ApplicationTestData.CreateContract();
        var store = new FakeCancellationRequestStore();
        var repository = new FakeContractRepository(contract);
        var handler = new CreateCancellationRequestHandler(
            repository,
            store,
            new MutableTimeProvider(FrozenUtc));

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(
                new CreateCancellationRequestCommand(contract.Id, key!),
                CancellationToken.None));

        Assert.Equal("idempotencyKey", exception.Field);
        Assert.Equal(0, repository.CallCount);
        Assert.Equal(0, store.FindCallCount);
        Assert.Equal(0, store.TryCreateCallCount);
    }

    [Fact]
    public async Task HandleAsync_rejects_idempotency_key_longer_than_128_characters()
    {
        var contract = ApplicationTestData.CreateContract();
        var store = new FakeCancellationRequestStore();
        var repository = new FakeContractRepository(contract);
        var handler = new CreateCancellationRequestHandler(
            repository,
            store,
            new MutableTimeProvider(FrozenUtc));

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(
                new CreateCancellationRequestCommand(contract.Id, new string('a', 129)),
                CancellationToken.None));

        Assert.Equal("idempotencyKey", exception.Field);
        Assert.Equal(0, repository.CallCount);
        Assert.Equal(0, store.FindCallCount);
    }

    private static CreateCancellationRequestHandler CreateHandler(
        Contract contract,
        FakeCancellationRequestStore store,
        TimeProvider timeProvider) =>
        new(new FakeContractRepository(contract), store, timeProvider);
}
