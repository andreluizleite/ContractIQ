namespace ContractIQ.Application.Knowledge;

public sealed record KnowledgeEvidence(
    Guid ChunkId,
    string DocumentKey,
    string Title,
    KnowledgeDocumentType DocumentType,
    string Version,
    string Language,
    Guid? CustomerId,
    Guid? ContractId,
    DateOnly EffectiveFrom,
    string SourcePath,
    string Section,
    int Page,
    string Content,
    double Score,
    double? LexicalScore,
    double? VectorScore);
