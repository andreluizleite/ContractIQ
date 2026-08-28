namespace ContractIQ.Application.Knowledge;

public interface IKnowledgeEmbeddingGenerator
{
    string ModelId { get; }

    int Dimensions { get; }

    Task<IReadOnlyList<float[]>> GenerateAsync(
        IReadOnlyList<string> values,
        CancellationToken cancellationToken = default);
}
