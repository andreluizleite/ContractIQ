namespace ContractIQ.Application.Assistant;

public sealed record AssistantCitation(
    int Number,
    string DocumentKey,
    string Title,
    string Version,
    string Section,
    int Page,
    string SourcePath);
