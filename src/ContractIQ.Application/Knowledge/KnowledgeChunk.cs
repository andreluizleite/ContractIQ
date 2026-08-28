namespace ContractIQ.Application.Knowledge;

public sealed record KnowledgeChunk(
    int Index,
    string Section,
    int Page,
    string Content,
    string Checksum,
    float[] Embedding);
