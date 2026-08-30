using System.Text;
using System.Text.Json;

namespace ContractIQ.AiEvaluator;

public static class EvaluationReportWriter
{
    public static async Task WriteAsync(
        AiEvaluationReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        string jsonPath = Path.Combine(outputDirectory, "report.json");
        string markdownPath = Path.Combine(outputDirectory, "report.md");

        await using (FileStream stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                report,
                EvaluationJson.Options,
                cancellationToken);
        }

        await File.WriteAllTextAsync(
            markdownPath,
            CreateMarkdown(report),
            cancellationToken);
    }

    private static string CreateMarkdown(AiEvaluationReport report)
    {
        var markdown = new StringBuilder()
            .AppendLine("# ContractIQ AI evaluation report")
            .AppendLine()
            .AppendLine($"- Mode: `{report.Mode}`")
            .AppendLine($"- Dataset: `{report.DatasetSchemaVersion}`")
            .AppendLine($"- Generated (UTC): `{report.GeneratedAtUtc:O}`")
            .AppendLine($"- Provider: `{report.Provider ?? "not-applicable"}`")
            .AppendLine($"- Model: `{report.ModelId ?? "not-invoked"}`")
            .AppendLine($"- Result: **{(report.Passed ? "PASS" : "FAIL")}**")
            .AppendLine($"- Scenarios: {report.PassedScenarios}/{report.TotalScenarios} passed")
            .AppendLine($"- Critical failures: {report.CriticalFailures}")
            .AppendLine()
            .AppendLine("## System safety gates")
            .AppendLine()
            .AppendLine("| Scenario | Language | Result | Failed metrics |")
            .AppendLine("| --- | --- | --- | --- |");

        foreach (ScenarioEvaluation scenario in report.Scenarios)
        {
            string failures = string.Join(
                ", ",
                scenario.Findings.Where(finding => !finding.Passed)
                    .Select(finding => finding.Metric));
            markdown.AppendLine(
                $"| {scenario.ScenarioId} | {scenario.Language} | " +
                $"{(scenario.Passed ? "PASS" : "FAIL")} | " +
                $"{(failures.Length == 0 ? "—" : failures)} |");
        }

        markdown
            .AppendLine()
            .AppendLine("## Interpretation")
            .AppendLine()
            .AppendLine(report.Mode == "offline"
                ? "Offline results validate deterministic orchestration and safety contracts against a versioned baseline. They do not measure the semantic quality of a live LLM."
                : "Live results apply deterministic safety gates to responses from the API's configured model. They are observations and are not a required CI gate.")
            .AppendLine()
            .AppendLine("The report intentionally excludes prompts, document content, generated answers, and credentials.");

        return markdown.ToString();
    }
}
