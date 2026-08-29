using ContractIQ.Application.Assistant.Tools;

namespace ContractIQ.Application.Assistant;

public sealed record GeneratedAssistantAnswer(
    string Text,
    string ModelId,
    AssistantActionProposal? ProposedAction = null);
