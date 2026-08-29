using ContractIQ.Application.Assistant.Tools;
using ContractIQ.Application.Cancellations.CreateCancellationRequest;
using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Contracts.AssessCancellation;
using ContractIQ.Application.Contracts.GetContractDetails;
using ContractIQ.Application.Knowledge;
using ContractIQ.Application.Knowledge.Search;
using Xunit;

namespace ContractIQ.Application.Tests.Assistant;

public sealed class CancellationAssistantToolsTests
{
    private static readonly DateTimeOffset FrozenUtc =
        DateTimeOffset.Parse("2026-03-01T00:30:00Z");

    [Fact]
    public async Task Preparation_returns_deterministic_preview_without_changing_state()
    {
        var contract = ApplicationTestData.CreateContract();
        var repository = new FakeContractRepository(contract);
        var timeProvider = new MutableTimeProvider(FrozenUtc);
        var audit = new FakeAssistantToolAudit();
        var tools = new ContractAssistantReadTools(
            new GetContractDetailsHandler(repository),
            new AssessCancellationHandler(repository, timeProvider),
            new EmptyKnowledgeSearch(),
            audit,
            timeProvider);

        AssistantActionProposal proposal = await tools.PrepareCancellationAsync(
            Context(contract.Id, contract.CustomerId),
            AssistantToolNames.CreateCancellation);

        Assert.Equal(AssistantToolNames.CreateCancellation, proposal.Name);
        Assert.True(proposal.RequiresConfirmation);
        Assert.True(proposal.CanExecute);
        Assert.Equal(2_500m, proposal.Assessment.Penalty.Amount);
        Assert.Contains(
            audit.Events,
            item => item.ToolName == AssistantToolNames.PrepareCancellation &&
                item.Outcome == "confirmation_required" &&
                !item.StateChanging);
    }

    [Fact]
    public async Task Write_tool_rejects_missing_confirmation_before_accessing_state()
    {
        var contract = ApplicationTestData.CreateContract();
        var repository = new FakeContractRepository(contract);
        var store = new FakeCancellationRequestStore();
        var audit = new FakeAssistantToolAudit();
        var handler = CreateWriteHandler(repository, store, audit);

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(Command(contract, confirmed: false, "request-001")));

        Assert.Equal("Confirmed", exception.Field);
        Assert.Equal(0, repository.CallCount);
        Assert.Empty(store.Requests);
        Assert.Contains(audit.Events, item => item.Outcome == "confirmation_missing");
    }

    [Fact]
    public async Task Write_tool_rejects_an_unrecognized_model_intent()
    {
        var contract = ApplicationTestData.CreateContract();
        var repository = new FakeContractRepository(contract);
        var store = new FakeCancellationRequestStore();
        var audit = new FakeAssistantToolAudit();
        var handler = CreateWriteHandler(repository, store, audit);
        ConfirmCancellationActionCommand command = Command(
            contract,
            confirmed: true,
            "request-001") with
        {
            Intent = "set_penalty_to_zero",
        };

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(command));

        Assert.Equal("Intent", exception.Field);
        Assert.Equal(0, repository.CallCount);
        Assert.Empty(store.Requests);
        Assert.Contains(audit.Events, item => item.Outcome == "invalid_intent");
    }

    [Fact]
    public async Task Write_tool_creates_once_and_replays_the_same_idempotency_key()
    {
        var contract = ApplicationTestData.CreateContract();
        var repository = new FakeContractRepository(contract);
        var store = new FakeCancellationRequestStore();
        var audit = new FakeAssistantToolAudit();
        var handler = CreateWriteHandler(repository, store, audit);
        ConfirmCancellationActionCommand command = Command(
            contract,
            confirmed: true,
            "request-001");

        CreateCancellationRequestResult created = await handler.HandleAsync(command);
        CreateCancellationRequestResult replayed = await handler.HandleAsync(command);

        Assert.False(created.IsReplay);
        Assert.True(replayed.IsReplay);
        Assert.Equal(created.Request, replayed.Request);
        Assert.Single(store.Requests);
        Assert.Contains(audit.Events, item => item.Outcome == "created");
        Assert.Contains(audit.Events, item => item.Outcome == "replayed");
    }

    [Fact]
    public async Task Write_tool_rejects_a_duplicate_open_request_with_a_new_key()
    {
        var contract = ApplicationTestData.CreateContract();
        var repository = new FakeContractRepository(contract);
        var store = new FakeCancellationRequestStore();
        var audit = new FakeAssistantToolAudit();
        var handler = CreateWriteHandler(repository, store, audit);

        await handler.HandleAsync(Command(contract, confirmed: true, "request-001"));
        var exception = await Assert.ThrowsAsync<ApplicationConflictException>(
            () => handler.HandleAsync(Command(contract, confirmed: true, "request-002")));

        Assert.Equal("cancellation_request_already_open", exception.Code);
        Assert.Single(store.Requests);
        Assert.Contains(audit.Events, item => item.Outcome == "rejected");
    }

    [Fact]
    public async Task Write_tool_hides_a_contract_outside_the_customer_scope()
    {
        var contract = ApplicationTestData.CreateContract();
        var repository = new FakeContractRepository(contract);
        var store = new FakeCancellationRequestStore();
        var audit = new FakeAssistantToolAudit();
        var handler = CreateWriteHandler(repository, store, audit);
        ConfirmCancellationActionCommand command = Command(
            contract,
            confirmed: true,
            "request-001") with
        {
            CustomerId = Guid.NewGuid(),
        };

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => handler.HandleAsync(command));

        Assert.Empty(store.Requests);
        Assert.Contains(audit.Events, item => item.Outcome == "rejected");
    }

    private static ConfirmCancellationActionHandler CreateWriteHandler(
        FakeContractRepository repository,
        FakeCancellationRequestStore store,
        FakeAssistantToolAudit audit)
    {
        var timeProvider = new MutableTimeProvider(FrozenUtc);
        return new ConfirmCancellationActionHandler(
            repository,
            new CreateCancellationRequestHandler(repository, store, timeProvider),
            new ImmediateAssistantWriteTransaction(),
            audit,
            timeProvider);
    }

    private static ConfirmCancellationActionCommand Command(
        ContractIQ.Domain.Contracts.Contract contract,
        bool confirmed,
        string idempotencyKey) =>
        new(
            contract.CustomerId,
            contract.Id,
            AssistantToolNames.CreateCancellation,
            confirmed,
            idempotencyKey);

    private static AssistantToolContext Context(Guid contractId, Guid customerId) =>
        new(
            "Create the cancellation request.",
            customerId,
            contractId,
            "en",
            new DateOnly(2026, 3, 1));

    private sealed class EmptyKnowledgeSearch : IKnowledgeSearch
    {
        public Task<IReadOnlyList<KnowledgeEvidence>> HandleAsync(
            SearchKnowledgeQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeEvidence>>([]);
    }

    private sealed class FakeAssistantToolAudit : IAssistantToolAudit
    {
        public List<AssistantToolAuditEvent> Events { get; } = [];

        public Task RecordAsync(
            AssistantToolAuditEvent auditEvent,
            CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class ImmediateAssistantWriteTransaction : IAssistantWriteTransaction
    {
        public Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }
}
