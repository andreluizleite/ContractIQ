using ContractIQ.Infrastructure.Assistant;
using Xunit;

namespace ContractIQ.IntegrationTests;

public sealed class FoundryChatCompatibilityTests
{
    [Fact]
    public void Uses_the_gpt5_compatible_completion_token_limit()
    {
        OpenAI.Chat.ChatCompletionOptions options =
            ChatClientAssistantAnswerGenerator.CreateFoundryChatOptions(350);

        Assert.Equal(350, options.MaxOutputTokenCount);
    }
}
