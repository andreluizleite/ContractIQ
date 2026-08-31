using System.ClientModel.Primitives;
using Azure.Core;
using OpenAI;

namespace ContractIQ.Infrastructure.AI;

/// <summary>
/// Creates OpenAI-compatible clients for a Microsoft Foundry resource without
/// storing an API key. DefaultAzureCredential is supplied by dependency
/// injection so local Azure CLI credentials and future managed identities use
/// the same application code.
/// </summary>
internal sealed class FoundryOpenAIClientFactory(
    TokenCredential credential,
    FoundryClientOptions options)
{
    private const string FoundryTokenScope = "https://ai.azure.com/.default";

    public OpenAIClient Create(Uri endpoint)
    {
        var tokenPolicy = new BearerTokenPolicy(credential, FoundryTokenScope);
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = endpoint,
            RetryPolicy = new ClientRetryPolicy(options.MaximumRetries),
        };

        // The official Foundry keyless .NET pattern currently exposes this
        // authentication-policy constructor behind OPENAI001. Keep the
        // acknowledgement local so other experimental APIs still fail builds.
#pragma warning disable OPENAI001
        var client = new OpenAIClient(
            authenticationPolicy: tokenPolicy,
            options: clientOptions);
#pragma warning restore OPENAI001

        return client;
    }
}
