using System.Net;
using System.Text;
using System.Text.Json;
using ContractIQ.AiEvaluator;
using ContractIQ.Application.Assistant;
using ContractIQ.Application.Contracts.AssessCancellation;
using Xunit;

namespace ContractIQ.AI.Evaluations;

public sealed class LiveEvaluationClientTests
{
    [Fact]
    public async Task Live_client_uses_only_canonical_read_and_answer_endpoints()
    {
        (EvaluationDataset dataset, EvaluationBaseline baseline) =
            await EvaluationTestData.LoadAsync();
        EvaluationScenario scenario = dataset.Scenarios.Single(item =>
            item.Id == "acme-penalty-en");
        var host = new OfflineEvaluationHost(
            baseline.CapturedAsOf,
            dataset,
            baseline.Responses);
        OfflineScenarioExecution baselineResponse = await host.ExecuteAsync(scenario);
        var handler = new RecordingHandler(baselineResponse);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5186/"),
        };
        var client = new LiveEvaluationClient(httpClient);

        CancellationAssessmentDto assessment =
            await client.GetCanonicalAssessmentAsync(scenario, CancellationToken.None);
        ContractAnswer answer = await client.AskAsync(scenario, CancellationToken.None);

        Assert.Equal(baselineResponse.CanonicalAssessment, assessment);
        Assert.Equal(baselineResponse.Answer.Answer, answer.Answer);
        Assert.Equal(baselineResponse.Answer.Language, answer.Language);
        Assert.Equal(
            baselineResponse.Answer.HasSufficientEvidence,
            answer.HasSufficientEvidence);
        Assert.Equal(baselineResponse.Answer.Assessment, answer.Assessment);
        Assert.Equal(
            baselineResponse.Answer.Citations.ToArray(),
            answer.Citations.ToArray());
        Assert.Equal(baselineResponse.Answer.ModelId, answer.ModelId);
        Assert.Equal(baselineResponse.Answer.ProposedAction, answer.ProposedAction);
        Assert.Collection(
            handler.Requests,
            request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Contains("cancellation-assessment", request.Path);
            },
            request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/api/v1/assistant/answers", request.Path);
            });
        Assert.DoesNotContain(handler.Requests, request =>
            request.Path.Contains("/assistant/actions/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Live_runner_records_provider_failure_and_continues()
    {
        (EvaluationDataset dataset, EvaluationBaseline baseline) =
            await EvaluationTestData.LoadAsync();
        var host = new OfflineEvaluationHost(
            baseline.CapturedAsOf,
            dataset,
            baseline.Responses);
        EvaluationScenario successfulScenario = dataset.Scenarios[1];
        OfflineScenarioExecution successful = await host.ExecuteAsync(successfulScenario);
        var client = new PartiallyFailingClient(
            dataset.Scenarios[0].Id,
            successfulScenario.Id,
            successful);
        var runner = new EvaluationRunner(new ContractAnswerEvaluator());

        AiEvaluationReport report = await runner.RunLiveAsync(
            dataset with { Scenarios = dataset.Scenarios.Take(2).ToArray() },
            client,
            provider: "MicrosoftFoundry",
            deployment: "contractiq-chat");

        Assert.False(report.Passed);
        Assert.Equal(2, report.TotalScenarios);
        Assert.Equal("MicrosoftFoundry", report.Provider);
        Assert.Equal("contractiq-chat", report.Deployment);
        Assert.Contains(report.Scenarios, result =>
            result.ScenarioId == dataset.Scenarios[0].Id &&
            result.Findings.Any(finding => finding.Metric == "provider_request"));
        Assert.Contains(report.Scenarios, result =>
            result.ScenarioId == successfulScenario.Id && result.Passed);
    }

    [Fact]
    public async Task Live_runner_excludes_synthetic_offline_only_scenarios()
    {
        (EvaluationDataset dataset, _) = await EvaluationTestData.LoadAsync();
        EvaluationScenario[] offlineOnly = dataset.Scenarios
            .Where(item => item.OfflineOnly)
            .ToArray();
        var client = new CountingClient();
        var runner = new EvaluationRunner(new ContractAnswerEvaluator());

        AiEvaluationReport report = await runner.RunLiveAsync(
            dataset with { Scenarios = offlineOnly },
            client);

        Assert.Equal(2, offlineOnly.Length);
        Assert.Equal(0, report.TotalScenarios);
        Assert.Equal(0, client.CallCount);
    }

    private sealed class RecordingHandler(OfflineScenarioExecution baselineResponse)
        : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            Requests.Add((request.Method, path));
            object payload = request.Method == HttpMethod.Get
                ? baselineResponse.CanonicalAssessment
                : baselineResponse.Answer;
            string json = JsonSerializer.Serialize(payload, EvaluationJson.Options);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
        }
    }

    private sealed class PartiallyFailingClient(
        string failingScenarioId,
        string successfulScenarioId,
        OfflineScenarioExecution successful) : ILiveEvaluationClient
    {
        public Task<CancellationAssessmentDto> GetCanonicalAssessmentAsync(
            EvaluationScenario scenario,
            CancellationToken cancellationToken) => scenario.Id == failingScenarioId
                ? Task.FromException<CancellationAssessmentDto>(new HttpRequestException("Unavailable"))
                : Task.FromResult(successful.CanonicalAssessment);

        public Task<ContractAnswer> AskAsync(
            EvaluationScenario scenario,
            CancellationToken cancellationToken)
        {
            Assert.Equal(successfulScenarioId, scenario.Id);
            return Task.FromResult(successful.Answer);
        }
    }

    private sealed class CountingClient : ILiveEvaluationClient
    {
        public int CallCount { get; private set; }

        public Task<CancellationAssessmentDto> GetCanonicalAssessmentAsync(
            EvaluationScenario scenario,
            CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException("An offline-only scenario reached the live client.");
        }

        public Task<ContractAnswer> AskAsync(
            EvaluationScenario scenario,
            CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException("An offline-only scenario reached the live client.");
        }
    }
}
