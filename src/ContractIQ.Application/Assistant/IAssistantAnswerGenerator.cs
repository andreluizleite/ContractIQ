using ContractIQ.Application.Assistant.Tools;

namespace ContractIQ.Application.Assistant;

public interface IAssistantAnswerGenerator
{
    Task<GeneratedAssistantAnswer> GenerateAsync(
        AssistantPrompt prompt,
        AssistantToolContext toolContext,
        CancellationToken cancellationToken = default);
}
