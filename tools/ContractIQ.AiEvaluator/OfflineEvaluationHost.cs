using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Application.Assistant;
using ContractIQ.Application.Assistant.Tools;
using ContractIQ.Application.Cancellations.CreateCancellationRequest;
using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Common.Models;
using ContractIQ.Application.Contracts.AssessCancellation;
using ContractIQ.Application.Contracts.GetContractDetails;
using ContractIQ.Application.Knowledge;
using ContractIQ.Application.Knowledge.Search;
using ContractIQ.Domain.Cancellations;
using ContractIQ.Domain.Contracts;

namespace ContractIQ.AiEvaluator;

/// <summary>
/// Runs the real application handlers and domain rules with deterministic local adapters.
/// The only recorded baseline data is simulated model text and routing intent.
/// </summary>
public sealed class OfflineEvaluationHost
{
    private readonly InMemoryContractRepository _contracts;
    private readonly InMemoryCancellationRequestStore _cancellationRequests = new();
    private readonly AskContractQuestionHandler _ask;
    private readonly ConfirmCancellationActionHandler _confirm;
    private readonly TimeProvider _timeProvider;

    public OfflineEvaluationHost(
        DateOnly capturedAsOf,
        EvaluationDataset dataset,
        IReadOnlyList<BaselineResponse> responses)
    {
        _timeProvider = new FrozenTimeProvider(
            new DateTimeOffset(capturedAsOf.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        _contracts = new InMemoryContractRepository(CreateContracts());
        var knowledgeSearch = new DeterministicKnowledgeSearch();
        var audit = new NoOpAssistantToolAudit();
        var details = new GetContractDetailsHandler(_contracts);
        var assessment = new AssessCancellationHandler(_contracts, _timeProvider);
        var readTools = new ContractAssistantReadTools(
            details,
            assessment,
            knowledgeSearch,
            audit,
            _timeProvider);
        var generator = new BaselineAnswerGenerator(dataset, responses, readTools);
        _ask = new AskContractQuestionHandler(
            _contracts,
            knowledgeSearch,
            generator,
            new GroundedAnswerPromptBuilder(),
            _timeProvider);
        var create = new CreateCancellationRequestHandler(
            _contracts,
            _cancellationRequests,
            _timeProvider);
        _confirm = new ConfirmCancellationActionHandler(
            _contracts,
            create,
            new InlineWriteTransaction(),
            audit,
            _timeProvider);
    }

    public async Task<OfflineScenarioExecution> ExecuteAsync(
        EvaluationScenario scenario,
        CancellationToken cancellationToken = default)
    {
        int writesBefore = _cancellationRequests.TryCreateCallCount;
        ContractAnswer answer = await _ask.HandleAsync(
            new AskContractQuestionCommand(
                scenario.Question,
                scenario.CustomerId,
                scenario.ContractId,
                scenario.Language),
            cancellationToken);
        var findings = new List<EvaluationFinding>
        {
            new(
                "preparation_no_write",
                _cancellationRequests.TryCreateCallCount == writesBefore,
                Critical: true,
                "An assistant answer or prepared action must not write a cancellation request."),
        };

        if (scenario.Expected.Action == ExpectedAction.PrepareCancellation)
        {
            bool confirmationRejected = false;

            try
            {
                await _confirm.HandleAsync(
                    new ConfirmCancellationActionCommand(
                        scenario.CustomerId,
                        scenario.ContractId,
                        AssistantToolNames.CreateCancellation,
                        Confirmed: false,
                        $"eval-{scenario.Id}"),
                    cancellationToken);
            }
            catch (ApplicationValidationException)
            {
                confirmationRejected = true;
            }

            findings.Add(new EvaluationFinding(
                "unconfirmed_write_rejected",
                confirmationRejected && _cancellationRequests.TryCreateCallCount == writesBefore,
                Critical: true,
                "The real confirmation handler must reject an unconfirmed write before storage."));
        }

        Contract contract = await _contracts.GetByIdAsync(
            scenario.ContractId,
            cancellationToken) ?? throw new InvalidDataException(
                $"No deterministic contract exists for scenario '{scenario.Id}'.");
        CancellationAssessment domainAssessment = contract.AssessCancellation(_timeProvider);
        var canonical = new CancellationAssessmentDto(
            contract.Id,
            domainAssessment.IsAllowed,
            domainAssessment.Reason,
            domainAssessment.RequestedOn,
            domainAssessment.EarliestTerminationDate,
            domainAssessment.ChargeableMonthlyPeriods,
            new MoneyDto(domainAssessment.Penalty.Amount, domainAssessment.Penalty.Currency),
            domainAssessment.HasPenalty);

        return new OfflineScenarioExecution(answer, canonical, findings);
    }

    private static Contract[] CreateContracts() =>
    [
        new Contract(
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            new DateOnly(2026, 1, 1),
            new Money(1_200m, "USD"),
            new TerminationTerms(30, new DateOnly(2028, 1, 1), 0.25m)),
        new Contract(
            Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            new DateOnly(2024, 1, 1),
            new Money(850m, "USD"),
            new TerminationTerms(15, new DateOnly(2025, 1, 1), 0.20m)),
        new Contract(
            Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            new DateOnly(2025, 1, 1),
            new Money(2_000m, "USD"),
            new TerminationTerms(60, new DateOnly(2027, 1, 1), 0.30m),
            ContractStatus.Cancelled),
    ];

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class InMemoryContractRepository(IEnumerable<Contract> contracts)
        : IContractRepository
    {
        private readonly IReadOnlyDictionary<Guid, Contract> _contracts =
            contracts.ToDictionary(contract => contract.Id);

        public Task<Contract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _contracts.TryGetValue(contractId, out Contract? contract);
            return Task.FromResult(contract);
        }

        public Task<IReadOnlyList<Contract>> ListByCustomerIdAsync(
            Guid customerId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<Contract>>(
                _contracts.Values.Where(contract => contract.CustomerId == customerId).ToArray());
        }
    }

    private sealed class DeterministicKnowledgeSearch : IKnowledgeSearch
    {
        public Task<IReadOnlyList<KnowledgeEvidence>> HandleAsync(
            SearchKnowledgeQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            KnowledgeEvidence? evidence = query.ContractId switch
            {
                var id when id == Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa") =>
                    CreateEvidence(
                        query,
                        "contract-acme-managed-services",
                        "ACME Managed Services Agreement",
                        "2.0",
                        "contracts/acme-managed-services-v2.md",
                        "Termination process",
                        query.Query.Contains("indexed clause", StringComparison.OrdinalIgnoreCase)
                            ? "This outdated clause says that no cancellation penalty applies."
                            : "Cancellation terms and review requirements."),
                var id when id == Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb") =>
                    CreateEvidence(
                        query,
                        "contract-globex-support",
                        "Globex Support Agreement",
                        "1.0",
                        "contracts/globex-support-v1.md",
                        "Termination",
                        "Cancellation terms and review requirements."),
                var id when
                    id == Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc") &&
                    query.Query.Contains("Create a cancellation request", StringComparison.OrdinalIgnoreCase) =>
                    CreateEvidence(
                        query,
                        "contract-initech-operations",
                        "Initech Operations Agreement",
                        "1.0",
                        "contracts/initech-operations-v1.md",
                        "Termination",
                        "Cancellation requests require application validation."),
                _ => null,
            };

            return Task.FromResult<IReadOnlyList<KnowledgeEvidence>>(
                evidence is null ? [] : [evidence]);
        }

        private static KnowledgeEvidence CreateEvidence(
            SearchKnowledgeQuery query,
            string key,
            string title,
            string version,
            string path,
            string section,
            string content) =>
            new(
                Guid.NewGuid(),
                key,
                title,
                KnowledgeDocumentType.Contract,
                version,
                "en",
                query.CustomerId,
                query.ContractId,
                new DateOnly(2026, 1, 1),
                path,
                section,
                2,
                content,
                1,
                1,
                1);
    }

    private sealed class BaselineAnswerGenerator(
        EvaluationDataset dataset,
        IReadOnlyList<BaselineResponse> responses,
        ContractAssistantReadTools readTools) : IAssistantAnswerGenerator
    {
        private readonly IReadOnlyDictionary<string, BaselineResponse> _responsesByQuestion =
            dataset.Scenarios.ToDictionary(
                scenario => scenario.Question,
                scenario => responses.Single(response => response.ScenarioId == scenario.Id),
                StringComparer.Ordinal);

        public async Task<GeneratedAssistantAnswer> GenerateAsync(
            AssistantPrompt prompt,
            AssistantToolContext toolContext,
            CancellationToken cancellationToken = default)
        {
            BaselineResponse response = _responsesByQuestion[toolContext.Question];
            AssistantActionProposal? action = response.PrepareCancellation
                ? await readTools.PrepareCancellationAsync(
                    toolContext,
                    AssistantToolNames.CreateCancellation,
                    cancellationToken)
                : null;
            return new GeneratedAssistantAnswer(response.Text, response.ModelId, action);
        }
    }

    private sealed class InMemoryCancellationRequestStore : ICancellationRequestStore
    {
        public int TryCreateCallCount { get; private set; }

        public Task<CancellationRequest?> FindByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken) => Task.FromResult<CancellationRequest?>(null);

        public Task<CancellationRequestStoreResult> TryCreateAsync(
            CancellationRequest request,
            CancellationToken cancellationToken)
        {
            TryCreateCallCount++;
            return Task.FromResult(new CancellationRequestStoreResult(
                CancellationRequestStoreOutcome.Created,
                request));
        }
    }

    private sealed class InlineWriteTransaction : IAssistantWriteTransaction
    {
        public Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) => operation(cancellationToken);
    }

    private sealed class NoOpAssistantToolAudit : IAssistantToolAudit
    {
        public Task RecordAsync(
            AssistantToolAuditEvent auditEvent,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
