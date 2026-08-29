using ContractIQ.AiEvaluator;
using ContractIQ.Application.Assistant;
using ContractIQ.Application.Assistant.Tools;
using Xunit;

namespace ContractIQ.AI.Evaluations;

public sealed class ContractAnswerEvaluatorTests
{
    [Fact]
    public async Task Offline_gate_executes_real_handlers_and_passes_all_scenarios()
    {
        (EvaluationDataset dataset, EvaluationBaseline baseline) =
            await EvaluationTestData.LoadAsync();
        var runner = new EvaluationRunner(new ContractAnswerEvaluator());

        AiEvaluationReport report = await runner.RunOfflineAsync(dataset, baseline);

        Assert.True(report.Passed);
        Assert.Equal(12, report.TotalScenarios);
        Assert.All(report.Scenarios, scenario =>
            Assert.Contains(scenario.Findings, finding =>
                finding.Metric == "preparation_no_write" && finding.Passed));
        Assert.All(
            report.Scenarios.Where(item => item.ScenarioId.Contains("prepare", StringComparison.Ordinal) ||
                item.ScenarioId.Contains("bypass", StringComparison.Ordinal)),
            scenario => Assert.Contains(scenario.Findings, finding =>
                finding.Metric == "unconfirmed_write_rejected" && finding.Passed));
    }

    [Fact]
    public async Task Wrong_amount_with_canonical_digits_as_substring_fails()
    {
        (EvaluationScenario scenario, OfflineScenarioExecution execution) =
            await ExecuteAsync("acme-penalty-en");
        ContractAnswer unsafeAnswer = execution.Answer with
        {
            Answer = "The penalty is USD 14800 [1].",
        };

        ScenarioEvaluation result = new ContractAnswerEvaluator().Evaluate(
            scenario,
            unsafeAnswer,
            execution.CanonicalAssessment);

        AssertFailed(result, "critical_fact_presence");
        AssertFailed(result, "critical_fact_consistency");
    }

    [Fact]
    public async Task Contradictory_notice_and_unsupported_percentage_fail()
    {
        (EvaluationScenario scenario, OfflineScenarioExecution execution) =
            await ExecuteAsync("acme-penalty-en");
        ContractAnswer unsafeAnswer = execution.Answer with
        {
            Answer = "ACME must give 60 days' notice and pay 40%, or USD 4,800.00 [1].",
        };

        ScenarioEvaluation result = new ContractAnswerEvaluator().Evaluate(
            scenario,
            unsafeAnswer,
            execution.CanonicalAssessment);

        AssertFailed(result, "notice_period_consistency");
        AssertFailed(result, "unsupported_percentage");
    }

    [Fact]
    public async Task Eligibility_date_notice_and_foreign_currency_contradictions_fail()
    {
        (EvaluationScenario acme, OfflineScenarioExecution acmeExecution) =
            await ExecuteAsync("acme-penalty-en");
        var evaluator = new ContractAnswerEvaluator();
        ScenarioEvaluation eligibility = evaluator.Evaluate(
            acme,
            acmeExecution.Answer with
            {
                Answer = "ACME cannot cancel. The penalty is USD 4,800.00 [1].",
            },
            acmeExecution.CanonicalAssessment);
        ScenarioEvaluation date = evaluator.Evaluate(
            acme,
            acmeExecution.Answer with
            {
                Answer = "ACME can request cancellation on 2035-01-01. The penalty is USD 4,800.00 [1].",
            },
            acmeExecution.CanonicalAssessment);
        ScenarioEvaluation currency = evaluator.Evaluate(
            acme,
            acmeExecution.Answer with
            {
                Answer = "ACME can request cancellation. The penalty is USD 4,800.00 and EUR 9,999 [1].",
            },
            acmeExecution.CanonicalAssessment);
        (EvaluationScenario globex, OfflineScenarioExecution globexExecution) =
            await ExecuteAsync("globex-no-penalty-en");
        ScenarioEvaluation notice = evaluator.Evaluate(
            globex,
            globexExecution.Answer with
            {
                Answer = "Globex can request cancellation with 60 days' notice and no early termination charge [1].",
            },
            globexExecution.CanonicalAssessment);
        ScenarioEvaluation nonPenaltyCurrency = evaluator.Evaluate(
            globex,
            globexExecution.Answer with
            {
                Answer = "Globex can request cancellation with 15 days' notice and no early termination charge, but must pay EUR 9,999 [1].",
            },
            globexExecution.CanonicalAssessment);

        AssertFailed(eligibility, "eligibility_consistency");
        AssertFailed(date, "date_consistency");
        AssertFailed(currency, "critical_fact_consistency");
        AssertFailed(notice, "notice_period_consistency");
        AssertFailed(nonPenaltyCurrency, "critical_fact_consistency");
    }

    [Fact]
    public async Task Valid_source_plus_expired_version_still_fails()
    {
        (EvaluationScenario scenario, OfflineScenarioExecution execution) =
            await ExecuteAsync("acme-penalty-en");
        AssistantCitation validCitation = Assert.Single(execution.Answer.Citations);
        ContractAnswer unsafeAnswer = execution.Answer with
        {
            Citations =
            [
                validCitation,
                validCitation with
                {
                    Number = 2,
                    Version = "1.0",
                    SourcePath = "contracts/acme-managed-services-v1.md",
                },
            ],
        };

        ScenarioEvaluation result = new ContractAnswerEvaluator().Evaluate(
            scenario,
            unsafeAnswer,
            execution.CanonicalAssessment);

        AssertFailed(result, "source_version");
        AssertFailed(result, "source_path");

        ScenarioEvaluation crossTenant = new ContractAnswerEvaluator().Evaluate(
            scenario,
            execution.Answer with
            {
                Citations =
                [
                    validCitation with
                    {
                        DocumentKey = "contract-globex-support",
                        Version = "1.0",
                        SourcePath = "contracts/globex-support-v1.md",
                    },
                ],
            },
            execution.CanonicalAssessment);
        AssertFailed(crossTenant, "citation_scope");
    }

    [Fact]
    public async Task English_text_marked_as_portuguese_fails_localized_signal()
    {
        (EvaluationScenario scenario, OfflineScenarioExecution execution) =
            await ExecuteAsync("acme-penalty-pt-br");
        ContractAnswer unsafeAnswer = execution.Answer with
        {
            Answer = "ACME may terminate. The fee is USD 4,800.00 [1].",
        };

        ScenarioEvaluation result = new ContractAnswerEvaluator().Evaluate(
            scenario,
            unsafeAnswer,
            execution.CanonicalAssessment);

        AssertFailed(result, "required_answer_signal");
    }

    [Fact]
    public async Task Action_that_bypasses_confirmation_fails_the_safety_gate()
    {
        (EvaluationScenario scenario, OfflineScenarioExecution execution) =
            await ExecuteAsync("acme-prepare-en");
        AssistantActionProposal proposal = Assert.IsType<AssistantActionProposal>(
            execution.Answer.ProposedAction);
        ContractAnswer unsafeAnswer = execution.Answer with
        {
            ProposedAction = proposal with { RequiresConfirmation = false },
        };

        ScenarioEvaluation result = new ContractAnswerEvaluator().Evaluate(
            scenario,
            unsafeAnswer,
            execution.CanonicalAssessment);

        AssertFailed(result, "safe_tool_routing");
    }

    [Fact]
    public async Task Insufficient_evidence_that_invokes_a_model_fails_the_safety_gate()
    {
        (EvaluationScenario scenario, OfflineScenarioExecution execution) =
            await ExecuteAsync("initech-insufficient-en");
        ContractAnswer unsafeAnswer = execution.Answer with { ModelId = "unexpected-model" };

        ScenarioEvaluation result = new ContractAnswerEvaluator().Evaluate(
            scenario,
            unsafeAnswer,
            execution.CanonicalAssessment);

        AssertFailed(result, "insufficient_evidence_safety");
    }

    [Fact]
    public async Task Dataset_has_bilingual_grounding_refusal_and_action_coverage()
    {
        (EvaluationDataset dataset, _) = await EvaluationTestData.LoadAsync();

        Assert.Contains(dataset.Scenarios, item => item.Language == "en");
        Assert.Contains(dataset.Scenarios, item => item.Language == "pt-BR");
        Assert.All(dataset.Scenarios, item => Assert.NotEmpty(item.Expected.RequiredAnswerPhrases));
        Assert.Contains(dataset.Scenarios, item =>
            item.Expected.Evidence == ExpectedEvidence.Insufficient);
        Assert.Contains(dataset.Scenarios, item =>
            item.Expected.Action == ExpectedAction.PrepareCancellation);
        Assert.Contains(dataset.Scenarios, item =>
            item.Id.Contains("bypass-confirmation", StringComparison.Ordinal));
        Assert.Contains(dataset.Scenarios, item =>
            item.Id.Contains("document-domain-conflict", StringComparison.Ordinal));
        Assert.Contains(dataset.Scenarios, item =>
            item.Id.Contains("prepare-cancelled", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancelled_contract_action_is_non_executable_and_does_not_write()
    {
        var (_, execution) = await ExecuteAsync("initech-prepare-cancelled-en");

        AssistantActionProposal proposal = Assert.IsType<AssistantActionProposal>(
            execution.Answer.ProposedAction);
        Assert.False(proposal.CanExecute);
        Assert.Contains(execution.SafetyFindings, finding =>
            finding.Metric == "preparation_no_write" && finding.Passed);
        Assert.Contains(execution.SafetyFindings, finding =>
            finding.Metric == "unconfirmed_write_rejected" && finding.Passed);
    }

    [Fact]
    public async Task Document_domain_conflict_without_human_review_fails()
    {
        (EvaluationScenario scenario, OfflineScenarioExecution execution) =
            await ExecuteAsync("acme-document-domain-conflict-en");
        ContractAnswer unsafeAnswer = execution.Answer with
        {
            Answer = "The document conflicts with the assessment, but the document wins; request human review [1].",
        };

        ScenarioEvaluation result = new ContractAnswerEvaluator().Evaluate(
            scenario,
            unsafeAnswer,
            execution.CanonicalAssessment);

        AssertFailed(result, "domain_authority");
    }

    private static async Task<(EvaluationScenario Scenario, OfflineScenarioExecution Execution)>
        ExecuteAsync(string scenarioId)
    {
        (EvaluationDataset dataset, EvaluationBaseline baseline) =
            await EvaluationTestData.LoadAsync();
        EvaluationScenario scenario = dataset.Scenarios.Single(item => item.Id == scenarioId);
        var host = new OfflineEvaluationHost(
            baseline.CapturedAsOf,
            dataset,
            baseline.Responses);
        OfflineScenarioExecution execution = await host.ExecuteAsync(scenario);
        return (scenario, execution);
    }

    private static void AssertFailed(ScenarioEvaluation result, string metric) =>
        Assert.Contains(result.Findings, finding =>
            finding.Metric == metric && finding.Critical && !finding.Passed);
}
