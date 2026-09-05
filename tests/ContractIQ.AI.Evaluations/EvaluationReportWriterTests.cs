using ContractIQ.AiEvaluator;
using Xunit;

namespace ContractIQ.AI.Evaluations;

public sealed class EvaluationReportWriterTests
{
    [Fact]
    public async Task Report_is_sanitized_and_does_not_store_questions_or_answers()
    {
        (EvaluationDataset dataset, EvaluationBaseline baseline) =
            await EvaluationTestData.LoadAsync();
        var runner = new EvaluationRunner(new ContractAnswerEvaluator());
        AiEvaluationReport report = await runner.RunOfflineAsync(dataset, baseline);
        string outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "contractiq-ai-evaluation-tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            await EvaluationReportWriter.WriteAsync(report, outputDirectory);
            string json = await File.ReadAllTextAsync(
                Path.Combine(outputDirectory, "report.json"));
            string markdown = await File.ReadAllTextAsync(
                Path.Combine(outputDirectory, "report.md"));

            Assert.Contains("Provider: `deterministic-baseline`", markdown);
            Assert.Contains("Deployment: `deterministic-baseline-v2`", markdown);
            Assert.Contains("Prompt version: `grounded-answer-v1`", markdown);
            Assert.Contains("Run date (UTC):", markdown);
            Assert.Contains(dataset.Name, markdown);

            foreach (EvaluationScenario scenario in dataset.Scenarios)
            {
                Assert.DoesNotContain(scenario.Question, json, StringComparison.Ordinal);
                Assert.DoesNotContain(scenario.Question, markdown, StringComparison.Ordinal);
            }

            foreach (BaselineResponse response in baseline.Responses)
            {
                Assert.DoesNotContain(
                    response.Text,
                    json,
                    StringComparison.Ordinal);
                Assert.DoesNotContain(
                    response.Text,
                    markdown,
                    StringComparison.Ordinal);
            }
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}
