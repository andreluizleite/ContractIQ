namespace ContractIQ.Application.Knowledge.Search;

public interface IKnowledgeSearch
{
    Task<IReadOnlyList<KnowledgeEvidence>> HandleAsync(
        SearchKnowledgeQuery query,
        CancellationToken cancellationToken = default);
}
