using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Domain.Cancellations;
using ContractIQ.Domain.Contracts;
using ContractIQ.Domain.Customers;

namespace ContractIQ.Infrastructure.Persistence;

internal sealed class InMemoryDemoStore :
    ICustomerRepository,
    IContractRepository,
    ICancellationRequestStore
{
    private readonly object _cancellationRequestGate = new();
    private readonly IReadOnlyList<Customer> _customers;
    private readonly IReadOnlyDictionary<Guid, Contract> _contracts;
    private readonly Dictionary<string, CancellationRequest> _cancellationRequestsByIdempotencyKey =
        new(StringComparer.Ordinal);

    public InMemoryDemoStore()
    {
        _customers = Array.AsReadOnly(
            CreateCustomers()
                .OrderBy(customer => customer.Name, StringComparer.Ordinal)
                .ThenBy(customer => customer.Id)
                .ToArray());

        _contracts = CreateContracts().ToDictionary(contract => contract.Id);
    }

    public Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_customers);
    }

    public Task<Contract?> GetByIdAsync(
        Guid contractId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _contracts.TryGetValue(contractId, out Contract? contract);

        return Task.FromResult(contract);
    }

    public Task<CancellationRequest?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        lock (_cancellationRequestGate)
        {
            _cancellationRequestsByIdempotencyKey.TryGetValue(
                idempotencyKey,
                out CancellationRequest? request);

            return Task.FromResult(request);
        }
    }

    public Task<CancellationRequestStoreResult> TryCreateAsync(
        CancellationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        lock (_cancellationRequestGate)
        {
            if (_cancellationRequestsByIdempotencyKey.TryGetValue(
                    request.IdempotencyKey,
                    out CancellationRequest? requestWithSameKey))
            {
                CancellationRequestStoreOutcome outcome =
                    requestWithSameKey.ContractId == request.ContractId
                        ? CancellationRequestStoreOutcome.Replayed
                        : CancellationRequestStoreOutcome.IdempotencyKeyConflict;

                return Task.FromResult(
                    new CancellationRequestStoreResult(outcome, requestWithSameKey));
            }

            CancellationRequest? openRequest = _cancellationRequestsByIdempotencyKey.Values
                .FirstOrDefault(existing =>
                    existing.ContractId == request.ContractId && existing.IsOpen);

            if (openRequest is not null)
            {
                return Task.FromResult(
                    new CancellationRequestStoreResult(
                        CancellationRequestStoreOutcome.OpenRequestExists,
                        openRequest));
            }

            _cancellationRequestsByIdempotencyKey.Add(request.IdempotencyKey, request);

            return Task.FromResult(
                new CancellationRequestStoreResult(
                    CancellationRequestStoreOutcome.Created,
                    request));
        }
    }

    private static IEnumerable<Customer> CreateCustomers()
    {
        yield return new Customer(DemoDataIds.AcmeCustomer, "ACME Corporation");
        yield return new Customer(DemoDataIds.GlobexCustomer, "Globex Corporation");
        yield return new Customer(DemoDataIds.InitechCustomer, "Initech");
    }

    private static IEnumerable<Contract> CreateContracts()
    {
        yield return new Contract(
            DemoDataIds.AcmeActiveContract,
            DemoDataIds.AcmeCustomer,
            new DateOnly(2026, 1, 1),
            new Money(1_200m, "USD"),
            new TerminationTerms(
                noticePeriodDays: 30,
                minimumCommitmentEndDate: new DateOnly(2028, 1, 1),
                earlyTerminationPenaltyRate: 0.25m));

        yield return new Contract(
            DemoDataIds.GlobexActiveContract,
            DemoDataIds.GlobexCustomer,
            new DateOnly(2024, 1, 1),
            new Money(850m, "USD"),
            new TerminationTerms(
                noticePeriodDays: 15,
                minimumCommitmentEndDate: new DateOnly(2025, 1, 1),
                earlyTerminationPenaltyRate: 0.20m));

        yield return new Contract(
            DemoDataIds.InitechCancelledContract,
            DemoDataIds.InitechCustomer,
            new DateOnly(2025, 1, 1),
            new Money(2_000m, "USD"),
            new TerminationTerms(
                noticePeriodDays: 60,
                minimumCommitmentEndDate: new DateOnly(2027, 1, 1),
                earlyTerminationPenaltyRate: 0.30m),
            ContractStatus.Cancelled);

        yield return new Contract(
            DemoDataIds.InitechExpiredContract,
            DemoDataIds.InitechCustomer,
            new DateOnly(2023, 1, 1),
            new Money(1_500m, "USD"),
            new TerminationTerms(
                noticePeriodDays: 30,
                minimumCommitmentEndDate: new DateOnly(2024, 1, 1),
                earlyTerminationPenaltyRate: 0.15m),
            ContractStatus.Expired);
    }
}
