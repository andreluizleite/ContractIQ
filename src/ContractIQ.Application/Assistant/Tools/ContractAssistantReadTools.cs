using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Contracts.AssessCancellation;
using ContractIQ.Application.Contracts.GetContractDetails;
using ContractIQ.Application.Knowledge;
using ContractIQ.Application.Knowledge.Search;

namespace ContractIQ.Application.Assistant.Tools;

public sealed class ContractAssistantReadTools(
    GetContractDetailsHandler contractDetails,
    AssessCancellationHandler assessCancellation,
    IKnowledgeSearch knowledgeSearch,
    IAssistantToolAudit audit,
    TimeProvider timeProvider)
{
    public async Task<ContractDetailsDto> GetContractAsync(
        AssistantToolContext context,
        CancellationToken cancellationToken = default)
    {
        ContractDetailsDto contract = await GetScopedContractAsync(context, cancellationToken);
        await RecordAsync(AssistantToolNames.GetContract, context, "succeeded", cancellationToken);
        return contract;
    }

    public async Task<CancellationAssessmentDto> AssessCancellationAsync(
        AssistantToolContext context,
        CancellationToken cancellationToken = default)
    {
        await GetScopedContractAsync(context, cancellationToken);
        CancellationAssessmentDto assessment = await assessCancellation.HandleAsync(
            new AssessCancellationQuery(context.ContractId),
            cancellationToken);
        await RecordAsync(
            AssistantToolNames.AssessCancellation,
            context,
            assessment.IsAllowed ? "allowed" : "not_allowed",
            cancellationToken);
        return assessment;
    }

    public async Task<IReadOnlyList<KnowledgeEvidence>> SearchEvidenceAsync(
        AssistantToolContext context,
        CancellationToken cancellationToken = default)
    {
        await GetScopedContractAsync(context, cancellationToken);
        IReadOnlyList<KnowledgeEvidence> evidence = await knowledgeSearch.HandleAsync(
            new SearchKnowledgeQuery(
                context.Question,
                context.CustomerId,
                context.ContractId,
                context.AsOf,
                5),
            cancellationToken);
        await RecordAsync(
            AssistantToolNames.SearchEvidence,
            context,
            evidence.Count == 0 ? "no_evidence" : "evidence_found",
            cancellationToken);
        return evidence;
    }

    public async Task<AssistantActionProposal> PrepareCancellationAsync(
        AssistantToolContext context,
        string intent,
        CancellationToken cancellationToken = default)
    {
        string normalizedIntent = ValidateIntent(intent);
        CancellationAssessmentDto assessment = await AssessCancellationAsync(
            context,
            cancellationToken);

        var proposal = new AssistantActionProposal(
            AssistantToolNames.CreateCancellation,
            normalizedIntent,
            RequiresConfirmation: true,
            CanExecute: assessment.IsAllowed,
            assessment);

        await RecordAsync(
            AssistantToolNames.PrepareCancellation,
            context,
            proposal.CanExecute ? "confirmation_required" : "not_allowed",
            cancellationToken);
        return proposal;
    }

    private async Task<ContractDetailsDto> GetScopedContractAsync(
        AssistantToolContext context,
        CancellationToken cancellationToken)
    {
        ContractDetailsDto contract = await contractDetails.HandleAsync(
            new GetContractDetailsQuery(context.ContractId),
            cancellationToken);

        if (contract.CustomerId != context.CustomerId)
        {
            throw new ResourceNotFoundException("Contract", context.ContractId);
        }

        return contract;
    }

    private async Task RecordAsync(
        string toolName,
        AssistantToolContext context,
        string outcome,
        CancellationToken cancellationToken)
    {
        await audit.RecordAsync(
            new AssistantToolAuditEvent(
                Guid.NewGuid(),
                toolName,
                context.CustomerId,
                context.ContractId,
                outcome,
                StateChanging: false,
                timeProvider.GetUtcNow()),
            cancellationToken);
    }

    private static string ValidateIntent(string intent)
    {
        if (!string.Equals(
            intent?.Trim(),
            AssistantToolNames.CreateCancellation,
            StringComparison.Ordinal))
        {
            throw new ApplicationValidationException(
                nameof(intent),
                $"Intent must be '{AssistantToolNames.CreateCancellation}'.");
        }

        return AssistantToolNames.CreateCancellation;
    }
}
