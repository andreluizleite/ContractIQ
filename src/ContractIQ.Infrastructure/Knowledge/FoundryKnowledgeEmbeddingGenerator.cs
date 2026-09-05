using System.ClientModel;
using System.Diagnostics;
using Azure.Identity;
using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Common.Observability;
using ContractIQ.Application.Knowledge;
using ContractIQ.Infrastructure.AI;
using Microsoft.Extensions.AI;

namespace ContractIQ.Infrastructure.Knowledge;

internal sealed class FoundryKnowledgeEmbeddingGenerator : IKnowledgeEmbeddingGenerator
{
    private const string ProviderName = "foundry";
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    public FoundryKnowledgeEmbeddingGenerator(
        KnowledgeOptions options,
        FoundryOpenAIClientFactory clientFactory)
    {
        ModelId = options.EmbeddingModel;
        Dimensions = options.EmbeddingDimensions;
        _generator = clientFactory
            .Create(options.EmbeddingEndpoint)
            .GetEmbeddingClient(ModelId)
            .AsIEmbeddingGenerator(Dimensions);
    }

    public string ModelId { get; }

    public int Dimensions { get; }

    public async Task<IReadOnlyList<float[]>> GenerateAsync(
        IReadOnlyList<string> values,
        CancellationToken cancellationToken = default)
    {
        long startedAt = Stopwatch.GetTimestamp();
        using Activity? activity = ContractIqTelemetry.StartActivity(
            "contractiq.ai.embedding.request");
        activity?.SetTag("gen_ai.operation.name", "embeddings");
        activity?.SetTag("gen_ai.provider.name", ProviderName);
        activity?.SetTag("gen_ai.request.model", ModelId);

        try
        {
            GeneratedEmbeddings<Embedding<float>> embeddings = await _generator.GenerateAsync(
                values,
                new EmbeddingGenerationOptions
                {
                    ModelId = ModelId,
                    Dimensions = Dimensions,
                },
                cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
            ContractIqTelemetry.RecordEmbeddingRequest(
                ProviderName,
                ModelId,
                "succeeded",
                Stopwatch.GetElapsedTime(startedAt));

            return embeddings
                .Select(embedding => embedding.Vector.ToArray())
                .ToArray();
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            RecordFailure(activity, "cancelled", exception, startedAt);
            throw;
        }
        catch (OperationCanceledException exception)
        {
            RecordFailure(activity, "unavailable", exception, startedAt);
            throw CreateUnavailableException(exception);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            RecordFailure(activity, "unavailable", exception, startedAt);
            throw CreateUnavailableException(exception);
        }
        catch (Exception exception)
        {
            RecordFailure(activity, "failed", exception, startedAt);
            throw;
        }
    }

    private static bool IsUnavailable(Exception exception) =>
        exception is HttpRequestException or
            ClientResultException or
            AuthenticationFailedException or
            CredentialUnavailableException;

    private ExternalDependencyUnavailableException CreateUnavailableException(
        Exception exception) =>
        new(
            ProviderName,
            $"Microsoft Foundry is unavailable or embedding deployment '{ModelId}' cannot be accessed.",
            exception);

    private void RecordFailure(
        Activity? activity,
        string outcome,
        Exception exception,
        long startedAt)
    {
        ContractIqTelemetry.MarkError(activity, exception);
        ContractIqTelemetry.RecordEmbeddingRequest(
            ProviderName,
            ModelId,
            outcome,
            Stopwatch.GetElapsedTime(startedAt));
    }
}
