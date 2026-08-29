using System.Diagnostics;
using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Common.Observability;
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
        return await ExecuteToolAsync(
            AssistantToolNames.GetContract,
            context,
            token => GetScopedContractAsync(context, token),
            _ => "succeeded",
            cancellationToken);
    }

    public async Task<CancellationAssessmentDto> AssessCancellationAsync(
        AssistantToolContext context,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteToolAsync(
            AssistantToolNames.AssessCancellation,
            context,
            async token =>
            {
                await GetScopedContractAsync(context, token);
                return await assessCancellation.HandleAsync(
                    new AssessCancellationQuery(context.ContractId),
                    token);
            },
            assessment => assessment.IsAllowed ? "allowed" : "not_allowed",
            cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeEvidence>> SearchEvidenceAsync(
        AssistantToolContext context,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteToolAsync(
            AssistantToolNames.SearchEvidence,
            context,
            async token =>
            {
                await GetScopedContractAsync(context, token);
                return await knowledgeSearch.HandleAsync(
                    new SearchKnowledgeQuery(
                        context.Question,
                        context.CustomerId,
                        context.ContractId,
                        context.AsOf,
                        5),
                    token);
            },
            evidence => evidence.Count == 0 ? "no_evidence" : "evidence_found",
            cancellationToken);
    }

    public async Task<AssistantActionProposal> PrepareCancellationAsync(
        AssistantToolContext context,
        string intent,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteToolAsync(
            AssistantToolNames.PrepareCancellation,
            context,
            async token =>
            {
                string normalizedIntent = ValidateIntent(intent);
                CancellationAssessmentDto assessment = await AssessCancellationAsync(
                    context,
                    token);

                return new AssistantActionProposal(
                    AssistantToolNames.CreateCancellation,
                    normalizedIntent,
                    RequiresConfirmation: true,
                    CanExecute: assessment.IsAllowed,
                    assessment);
            },
            proposal => proposal.CanExecute ? "confirmation_required" : "not_allowed",
            cancellationToken);
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

    private async Task<T> ExecuteToolAsync<T>(
        string toolName,
        AssistantToolContext context,
        Func<CancellationToken, Task<T>> operation,
        Func<T, string> outcomeSelector,
        CancellationToken cancellationToken)
    {
        using Activity? activity = ContractIqTelemetry.StartActivity(
            "contractiq.assistant.tool");
        activity?.SetTag("gen_ai.tool.name", toolName);
        activity?.SetTag("contractiq.tool.state_changing", false);

        try
        {
            T result = await operation(cancellationToken);
            string outcome = outcomeSelector(result);

            activity?.SetTag("contractiq.outcome", outcome);
            activity?.SetStatus(ActivityStatusCode.Ok);
            await RecordAsync(toolName, context, outcome, cancellationToken);

            return result;
        }
        catch (Exception exception)
        {
            string outcome = exception is OperationCanceledException
                ? "cancelled"
                : "failed";

            activity?.SetTag("contractiq.outcome", outcome);
            ContractIqTelemetry.MarkError(activity, exception);

            // The audit implementation is local and non-blocking. A cancelled
            // model request should still leave an outcome for diagnosis.
            await RecordAsync(toolName, context, outcome, CancellationToken.None);
            throw;
        }
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
