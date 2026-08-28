using ContractIQ.Application.Assistant;
using ContractIQ.Application.Common.Exceptions;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models.Exceptions;

namespace ContractIQ.Infrastructure.Assistant;

internal sealed class OllamaAssistantAnswerGenerator : IAssistantAnswerGenerator, IDisposable
{
    private readonly IChatClient _chatClient;
    private readonly AssistantOptions _options;

    public OllamaAssistantAnswerGenerator(AssistantOptions options)
    {
        _options = options;
        _chatClient = new OllamaApiClient(options.OllamaEndpoint, options.ChatModel);
    }

    public async Task<GeneratedAssistantAnswer> GenerateAsync(
        AssistantPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ChatResponse response = await _chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, prompt.SystemPrompt),
                    new ChatMessage(ChatRole.User, prompt.UserPrompt),
                ],
                new ChatOptions
                {
                    ModelId = _options.ChatModel,
                    MaxOutputTokens = _options.MaximumOutputTokens,
                    Temperature = _options.Temperature,
                },
                cancellationToken);

            return new GeneratedAssistantAnswer(response.Text, _options.ChatModel);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or OllamaException)
        {
            throw new ExternalDependencyUnavailableException(
                "ollama",
                $"Ollama is unavailable or chat model '{_options.ChatModel}' is not installed.",
                exception);
        }
    }

    public void Dispose() => _chatClient.Dispose();
}
