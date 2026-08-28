namespace ContractIQ.Application.Knowledge;

public interface IKnowledgeDocumentCatalog
{
    Task<IReadOnlyList<KnowledgeDocumentSource>> ReadAllAsync(
        CancellationToken cancellationToken = default);
}
