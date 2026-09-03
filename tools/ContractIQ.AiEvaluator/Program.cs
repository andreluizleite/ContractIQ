using ContractIQ.AiEvaluator;

const string DatasetRelativePath = "evaluations/datasets/contract-assistant-v1.json";
const string BaselineRelativePath = "evaluations/baselines/contract-assistant-v1.responses.json";
const string OutputRelativePath = "TestResults/ai-evaluations";

try
{
    Dictionary<string, string> arguments = ParseArguments(args);
    string repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
    string mode = arguments.GetValueOrDefault("mode", "offline");
    string datasetPath = ResolvePath(
        repositoryRoot,
        arguments.GetValueOrDefault("dataset", DatasetRelativePath));
    string outputPath = ResolvePath(
        repositoryRoot,
        arguments.GetValueOrDefault("output", OutputRelativePath));

    EvaluationDataset dataset = await EvaluationJson.ReadAsync<EvaluationDataset>(datasetPath);
    dataset = SelectScenarios(dataset, arguments.GetValueOrDefault("scenario-ids"));
    var runner = new EvaluationRunner(new ContractAnswerEvaluator());
    AiEvaluationReport report;

    if (string.Equals(mode, "offline", StringComparison.OrdinalIgnoreCase))
    {
        string baselinePath = ResolvePath(
            repositoryRoot,
            arguments.GetValueOrDefault("baseline", BaselineRelativePath));
        EvaluationBaseline baseline =
            await EvaluationJson.ReadAsync<EvaluationBaseline>(baselinePath);
        baseline = SelectResponses(baseline, dataset);
        report = await runner.RunOfflineAsync(dataset, baseline);
    }
    else if (string.Equals(mode, "live", StringComparison.OrdinalIgnoreCase))
    {
        string baseUrl = arguments.GetValueOrDefault("base-url", "http://localhost:5186/");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? endpoint))
        {
            throw new ArgumentException("--base-url must be an absolute URL.");
        }

        using var httpClient = new HttpClient
        {
            BaseAddress = endpoint,
            Timeout = TimeSpan.FromSeconds(45),
        };
        report = await runner.RunLiveAsync(
            dataset,
            new LiveEvaluationClient(httpClient),
            arguments.GetValueOrDefault("provider", "configured-api-provider"),
            arguments.GetValueOrDefault("deployment"),
            ParseScenarioDelay(arguments.GetValueOrDefault("delay-seconds")));
    }
    else
    {
        throw new ArgumentException("--mode must be 'offline' or 'live'.");
    }

    await EvaluationReportWriter.WriteAsync(report, outputPath);
    Console.WriteLine(
        $"AI evaluations: {(report.Passed ? "PASS" : "FAIL")} " +
        $"({report.PassedScenarios}/{report.TotalScenarios} scenarios, " +
        $"{report.CriticalFailures} critical failures).");
    Console.WriteLine($"Reports: {Path.Combine(outputPath, "report.md")}");
    return report.Passed ? 0 : 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"AI evaluation runner failed: {exception.Message}");
    return 2;
}

static Dictionary<string, string> ParseArguments(string[] values)
{
    var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (int index = 0; index < values.Length; index += 2)
    {
        if (!values[index].StartsWith("--", StringComparison.Ordinal) ||
            index + 1 >= values.Length)
        {
            throw new ArgumentException(
                "Arguments must be supplied as '--name value' pairs.");
        }

        parsed[values[index][2..]] = values[index + 1];
    }

    return parsed;
}

static string FindRepositoryRoot(string startPath)
{
    DirectoryInfo? directory = new(startPath);

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

static string ResolvePath(string root, string path) => Path.IsPathRooted(path)
    ? Path.GetFullPath(path)
    : Path.GetFullPath(Path.Combine(root, path));

static EvaluationDataset SelectScenarios(
    EvaluationDataset dataset,
    string? selectedScenarioIds)
{
    if (string.IsNullOrWhiteSpace(selectedScenarioIds))
    {
        return dataset;
    }

    string[] requestedIds = selectedScenarioIds
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (requestedIds.Length == 0 ||
        requestedIds.Distinct(StringComparer.Ordinal).Count() != requestedIds.Length)
    {
        throw new ArgumentException(
            "--scenario-ids must contain unique comma-separated scenario ids.");
    }

    IReadOnlyDictionary<string, EvaluationScenario> available = dataset.Scenarios
        .ToDictionary(scenario => scenario.Id, StringComparer.Ordinal);
    string[] unknownIds = requestedIds
        .Where(id => !available.ContainsKey(id))
        .ToArray();
    if (unknownIds.Length > 0)
    {
        throw new ArgumentException(
            $"Unknown scenario ids: {string.Join(", ", unknownIds)}.");
    }

    return dataset with
    {
        Scenarios = requestedIds.Select(id => available[id]).ToArray(),
    };
}

static EvaluationBaseline SelectResponses(
    EvaluationBaseline baseline,
    EvaluationDataset dataset)
{
    HashSet<string> selectedIds = dataset.Scenarios
        .Select(scenario => scenario.Id)
        .ToHashSet(StringComparer.Ordinal);
    return baseline with
    {
        Responses = baseline.Responses
            .Where(response => selectedIds.Contains(response.ScenarioId))
            .ToArray(),
    };
}

static TimeSpan ParseScenarioDelay(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return TimeSpan.Zero;
    }

    if (!int.TryParse(value, out int seconds) || seconds is < 0 or > 300)
    {
        throw new ArgumentException(
            "--delay-seconds must be an integer between 0 and 300.");
    }

    return TimeSpan.FromSeconds(seconds);
}
