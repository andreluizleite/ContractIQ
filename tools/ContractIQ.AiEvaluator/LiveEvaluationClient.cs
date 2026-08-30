using System.Net.Http.Json;
using ContractIQ.Application.Assistant;
using ContractIQ.Application.Contracts.AssessCancellation;

namespace ContractIQ.AiEvaluator;

public interface ILiveEvaluationClient
{
    Task<CancellationAssessmentDto> GetCanonicalAssessmentAsync(
        EvaluationScenario scenario,
        CancellationToken cancellationToken);

    Task<ContractAnswer> AskAsync(
        EvaluationScenario scenario,
        CancellationToken cancellationToken);
}

public sealed class LiveEvaluationClient(HttpClient httpClient) : ILiveEvaluationClient
{
    public async Task<CancellationAssessmentDto> GetCanonicalAssessmentAsync(
        EvaluationScenario scenario,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(
            $"api/v1/contracts/{scenario.ContractId}/cancellation-assessment",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CancellationAssessmentDto>(
            EvaluationJson.Options,
            cancellationToken) ?? throw new InvalidDataException(
                $"Assessment response was empty for scenario '{scenario.Id}'.");
    }

    public async Task<ContractAnswer> AskAsync(
        EvaluationScenario scenario,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "api/v1/assistant/answers",
            new
            {
                scenario.Question,
                scenario.CustomerId,
                scenario.ContractId,
                scenario.Language,
            },
            EvaluationJson.Options,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ContractAnswer>(
            EvaluationJson.Options,
            cancellationToken) ?? throw new InvalidDataException(
                $"Assistant response was empty for scenario '{scenario.Id}'.");
    }
}
