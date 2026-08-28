namespace ContractIQ.Application.Knowledge.Search;

public sealed record SearchKnowledgeQuery(
    string Query,
    Guid CustomerId,
    Guid ContractId,
    DateOnly? AsOf = null,
    int Limit = 5);
