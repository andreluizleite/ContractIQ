namespace ContractIQ.Application.Knowledge;

public sealed record KnowledgeDocumentSource(
    string DocumentKey,
    string Title,
    KnowledgeDocumentType DocumentType,
    string Version,
    string Language,
    Guid? CustomerId,
    Guid? ContractId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string SourcePath,
    string Content);
