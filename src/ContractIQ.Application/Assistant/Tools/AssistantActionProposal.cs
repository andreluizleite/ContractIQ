using ContractIQ.Application.Contracts.AssessCancellation;

namespace ContractIQ.Application.Assistant.Tools;

public sealed record AssistantActionProposal(
    string Name,
    string Intent,
    bool RequiresConfirmation,
    bool CanExecute,
    CancellationAssessmentDto Assessment);
