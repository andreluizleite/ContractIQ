using ContractIQ.Application.Knowledge;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models.Exceptions;

namespace ContractIQ.Infrastructure.Knowledge;

internal sealed class OllamaKnowledgeEmbeddingGenerator : IKnowledgeEmbeddingGenerator
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    public OllamaKnowledgeEmbeddingGenerator(KnowledgeOptions options)
    {
        ModelId = options.EmbeddingModel;
        Dimensions = options.EmbeddingDimensions;
        _generator = new OllamaApiClient(options.EmbeddingEndpoint, ModelId);
    }

    public string ModelId { get; }

    public int Dimensions { get; }

    public async Task<IReadOnlyList<float[]>> GenerateAsync(
        IReadOnlyList<string> values,
        CancellationToken cancellationToken = default)
    {
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

            return embeddings
                .Select(embedding => embedding.Vector.ToArray())
                .ToArray();
        }
        catch (Exception exception) when (
            exception is HttpRequestException or OllamaException)
        {
            throw new ContractIQ.Application.Common.Exceptions.ExternalDependencyUnavailableException(
                "ollama",
                $"Ollama is unavailable or embedding model '{ModelId}' is not installed.",
                exception);
        }
    }
}
