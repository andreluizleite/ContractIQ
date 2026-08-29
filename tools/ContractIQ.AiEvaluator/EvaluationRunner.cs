using ContractIQ.Application.Assistant;
using ContractIQ.Application.Contracts.AssessCancellation;

namespace ContractIQ.AiEvaluator;

public sealed class EvaluationRunner(ContractAnswerEvaluator evaluator)
{
    public async Task<AiEvaluationReport> RunOfflineAsync(
        EvaluationDataset dataset,
        EvaluationBaseline baseline,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(
                dataset.SchemaVersion,
                baseline.DatasetSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Evaluation dataset and baseline schema versions do not match.");
        }

        Dictionary<string, BaselineResponse> responses = baseline.Responses
            .ToDictionary(response => response.ScenarioId, StringComparer.Ordinal);
        ValidateScenarioCoverage(dataset, responses.Keys);
        var host = new OfflineEvaluationHost(
            baseline.CapturedAsOf,
            dataset,
            baseline.Responses);
        var evaluations = new List<ScenarioEvaluation>(dataset.Scenarios.Count);

        foreach (EvaluationScenario scenario in dataset.Scenarios)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!responses.ContainsKey(scenario.Id))
            {
                throw new InvalidDataException(
                    $"Offline baseline does not contain scenario '{scenario.Id}'.");
            }

            OfflineScenarioExecution execution = await host.ExecuteAsync(
                scenario,
                cancellationToken);
            ScenarioEvaluation evaluation = evaluator.Evaluate(
                scenario,
                execution.Answer,
                execution.CanonicalAssessment);
            EvaluationFinding[] findings = evaluation.Findings
                .Concat(execution.SafetyFindings)
                .ToArray();
            evaluations.Add(evaluation with
            {
                Passed = findings.All(finding => finding.Passed),
                Findings = findings,
            });
        }

        return CreateReport(
            dataset,
            "offline",
            provider: "deterministic-baseline",
            modelId: string.Join(",", baseline.Responses
                .Select(item => item.ModelId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)),
            evaluations);
    }

    public async Task<AiEvaluationReport> RunLiveAsync(
        EvaluationDataset dataset,
        ILiveEvaluationClient client,
        CancellationToken cancellationToken = default)
    {
        var evaluations = new List<ScenarioEvaluation>(dataset.Scenarios.Count);
        var modelIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (EvaluationScenario scenario in dataset.Scenarios.Where(item => !item.OfflineOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                CancellationAssessmentDto canonicalAssessment =
                    await client.GetCanonicalAssessmentAsync(scenario, cancellationToken);
                ContractAnswer answer = await client.AskAsync(scenario, cancellationToken);
                if (!string.IsNullOrWhiteSpace(answer.ModelId))
                {
                    modelIds.Add(answer.ModelId);
                }

                evaluations.Add(evaluator.Evaluate(
                    scenario,
                    answer,
                    canonicalAssessment));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                evaluations.Add(new ScenarioEvaluation(
                    scenario.Id,
                    scenario.Language,
                    Passed: false,
                    [new EvaluationFinding(
                        "provider_request",
                        Passed: false,
                        Critical: true,
                        $"Live provider request failed ({exception.GetType().Name}).")]));
            }
        }

        return CreateReport(
            dataset,
            "live",
            provider: "configured-api-provider",
            modelId: modelIds.Count == 0
                ? null
                : string.Join(",", modelIds.Order(StringComparer.Ordinal)),
            evaluations);
    }

    private static void ValidateScenarioCoverage(
        EvaluationDataset dataset,
        IEnumerable<string> responseIds)
    {
        string[] scenarioIds = dataset.Scenarios.Select(item => item.Id).ToArray();
        if (scenarioIds.Distinct(StringComparer.Ordinal).Count() != scenarioIds.Length)
        {
            throw new InvalidDataException("Evaluation scenario ids must be unique.");
        }

        string[] baselineIds = responseIds.ToArray();
        if (baselineIds.Distinct(StringComparer.Ordinal).Count() != baselineIds.Length ||
            !scenarioIds.ToHashSet(StringComparer.Ordinal).SetEquals(baselineIds))
        {
            throw new InvalidDataException(
                "Offline baseline must contain exactly one response for every scenario.");
        }
    }

    private static AiEvaluationReport CreateReport(
        EvaluationDataset dataset,
        string mode,
        string? provider,
        string? modelId,
        IReadOnlyList<ScenarioEvaluation> evaluations)
    {
        int failedScenarios = evaluations.Count(result => !result.Passed);
        int criticalFailures = evaluations
            .SelectMany(result => result.Findings)
            .Count(finding => finding.Critical && !finding.Passed);

        return new AiEvaluationReport(
            SchemaVersion: "1.0",
            dataset.SchemaVersion,
            mode,
            DateTimeOffset.UtcNow,
            provider,
            modelId,
            evaluations.Count,
            evaluations.Count - failedScenarios,
            failedScenarios,
            criticalFailures,
            evaluations);
    }
}
