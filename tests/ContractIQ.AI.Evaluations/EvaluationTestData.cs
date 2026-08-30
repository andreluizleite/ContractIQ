using ContractIQ.AiEvaluator;

namespace ContractIQ.AI.Evaluations;

internal static class EvaluationTestData
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static async Task<(EvaluationDataset Dataset, EvaluationBaseline Baseline)> LoadAsync()
    {
        EvaluationDataset dataset = await EvaluationJson.ReadAsync<EvaluationDataset>(
            Path.Combine(
                RepositoryRoot,
                "evaluations",
                "datasets",
                "contract-assistant-v1.json"));
        EvaluationBaseline baseline = await EvaluationJson.ReadAsync<EvaluationBaseline>(
            Path.Combine(
                RepositoryRoot,
                "evaluations",
                "baselines",
                "contract-assistant-v1.responses.json"));

        return (dataset, baseline);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ContractIQ.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the ContractIQ repository root.");
    }
}
