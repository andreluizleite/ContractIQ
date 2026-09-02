using ContractIQ.Application.Assistant;
using ContractIQ.Application.Contracts.AssessCancellation;

namespace ContractIQ.AiEvaluator;

public enum ExpectedEvidence
{
    Sufficient,
    Insufficient,
}

public enum ExpectedAction
{
    None,
    PrepareCancellation,
}

public sealed record EvaluationDataset(
    string SchemaVersion,
    string Name,
    IReadOnlyList<EvaluationScenario> Scenarios);

public sealed record EvaluationScenario(
    string Id,
    string Description,
    string Question,
    string Language,
    Guid CustomerId,
    Guid ContractId,
    EvaluationExpectation Expected)
{
    public bool OfflineOnly { get; init; }
}

public sealed record EvaluationExpectation(
    ExpectedEvidence Evidence,
    ExpectedAction Action,
    IReadOnlyList<string> RequiredDocumentKeys,
    IReadOnlyDictionary<string, string> RequiredDocumentVersions,
    IReadOnlyList<string> AllowedDocumentKeys,
    IReadOnlyList<string> RequiredAnswerPhrases,
    bool RequiresPenaltyMention)
{
    public IReadOnlyDictionary<string, string> RequiredSourcePaths { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public bool RequiresDomainAuthority { get; init; }
}

public sealed record EvaluationBaseline(
    string DatasetSchemaVersion,
    DateOnly CapturedAsOf,
    IReadOnlyList<BaselineResponse> Responses);

public sealed record BaselineResponse(
    string ScenarioId,
    string Text,
    string ModelId,
    bool PrepareCancellation);

public sealed record OfflineScenarioExecution(
    ContractAnswer Answer,
    CancellationAssessmentDto CanonicalAssessment,
    IReadOnlyList<EvaluationFinding> SafetyFindings);

public sealed record EvaluationFinding(
    string Metric,
    bool Passed,
    bool Critical,
    string Message);

public sealed record ScenarioEvaluation(
    string ScenarioId,
    string Language,
    bool Passed,
    IReadOnlyList<EvaluationFinding> Findings);

public sealed record AiEvaluationReport(
    string SchemaVersion,
    string DatasetName,
    string DatasetSchemaVersion,
    string Mode,
    DateTimeOffset GeneratedAtUtc,
    string? Provider,
    string? Deployment,
    string? ModelId,
    string PromptVersion,
    int TotalScenarios,
    int PassedScenarios,
    int FailedScenarios,
    int CriticalFailures,
    IReadOnlyList<ScenarioEvaluation> Scenarios)
{
    public bool Passed => FailedScenarios == 0 && CriticalFailures == 0;
}
