using ContractIQ.Application.Contracts.AssessCancellation;

namespace ContractIQ.Application.Assistant;

public sealed record ContractAnswer(
    string Answer,
    string Language,
    bool HasSufficientEvidence,
    CancellationAssessmentDto Assessment,
    IReadOnlyList<AssistantCitation> Citations,
    string? ModelId);
