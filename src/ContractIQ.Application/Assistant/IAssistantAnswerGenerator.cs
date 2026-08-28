namespace ContractIQ.Application.Assistant;

public interface IAssistantAnswerGenerator
{
    Task<GeneratedAssistantAnswer> GenerateAsync(
        AssistantPrompt prompt,
        CancellationToken cancellationToken = default);
}
