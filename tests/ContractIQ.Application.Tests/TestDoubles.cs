using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Domain.Cancellations;
using ContractIQ.Domain.Contracts;
using ContractIQ.Domain.Customers;

namespace ContractIQ.Application.Tests;

internal sealed class FakeCustomerRepository(params Customer[] customers) : ICustomerRepository
{
    public Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Customer>>(customers);
    }
}

internal sealed class FakeContractRepository(params Contract[] contracts) : IContractRepository
{
    private readonly IReadOnlyDictionary<Guid, Contract> _contracts =
        contracts.ToDictionary(contract => contract.Id);

    public int CallCount { get; private set; }

    public Task<Contract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        _contracts.TryGetValue(contractId, out var contract);
        return Task.FromResult(contract);
    }

    public Task<IReadOnlyList<Contract>> ListByCustomerIdAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Contract> matches = _contracts.Values
            .Where(contract => contract.CustomerId == customerId)
            .OrderByDescending(contract => contract.StartDate)
            .ThenBy(contract => contract.Id)
            .ToArray();
        return Task.FromResult(matches);
    }
}

internal sealed class FakeCancellationRequestStore : ICancellationRequestStore
{
    private readonly Dictionary<string, CancellationRequest> _requestsByKey =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<CancellationRequest> Requests => _requestsByKey.Values;

    public int FindCallCount { get; private set; }

    public int TryCreateCallCount { get; private set; }

    public Task<CancellationRequest?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FindCallCount++;
        _requestsByKey.TryGetValue(idempotencyKey, out var request);
        return Task.FromResult(request);
    }

    public Task<CancellationRequestStoreResult> TryCreateAsync(
        CancellationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryCreateCallCount++;

        if (_requestsByKey.TryGetValue(request.IdempotencyKey, out var replay))
        {
            var replayOutcome = replay.ContractId == request.ContractId
                ? CancellationRequestStoreOutcome.Replayed
                : CancellationRequestStoreOutcome.IdempotencyKeyConflict;

            return Task.FromResult(new CancellationRequestStoreResult(replayOutcome, replay));
        }

        var openRequest = _requestsByKey.Values.FirstOrDefault(
            candidate => candidate.ContractId == request.ContractId && candidate.IsOpen);

        if (openRequest is not null)
        {
            return Task.FromResult(
                new CancellationRequestStoreResult(
                    CancellationRequestStoreOutcome.OpenRequestExists,
                    openRequest));
        }

        _requestsByKey.Add(request.IdempotencyKey, request);
        return Task.FromResult(
            new CancellationRequestStoreResult(CancellationRequestStoreOutcome.Created, request));
    }
}

internal sealed class MutableTimeProvider(
    DateTimeOffset utcNow,
    TimeZoneInfo? localTimeZone = null) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;

    public override TimeZoneInfo LocalTimeZone { get; } = localTimeZone ?? TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
}

internal static class ApplicationTestData
{
    public static readonly Guid AcmeCustomerId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static readonly Guid AcmeContractId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid GlobexContractId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static Contract CreateContract(
        Guid? id = null,
        Guid? customerId = null,
        ContractStatus status = ContractStatus.Active) =>
        new(
            id ?? AcmeContractId,
            customerId ?? AcmeCustomerId,
            new DateOnly(2026, 1, 1),
            new Money(1_000m, "BRL"),
            new TerminationTerms(
                noticePeriodDays: 30,
                minimumCommitmentEndDate: new DateOnly(2026, 12, 31),
                earlyTerminationPenaltyRate: 0.25m),
            status);
}
