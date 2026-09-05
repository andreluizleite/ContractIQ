using System.ClientModel.Primitives;
using ContractIQ.Infrastructure.Assistant;
using Xunit;

namespace ContractIQ.IntegrationTests;

public sealed class FoundryChatCompatibilityTests
{
    [Fact]
    public void Uses_gpt5_compatible_bounded_completion_options()
    {
        OpenAI.Chat.ChatCompletionOptions options =
            ChatClientAssistantAnswerGenerator.CreateFoundryChatOptions(600);
        string json = ModelReaderWriter.Write(
            options,
            ModelReaderWriterOptions.Json).ToString();

        Assert.Equal(600, options.MaxOutputTokenCount);
        Assert.Contains("\"reasoning_effort\":\"minimal\"", json);
    }
}
