using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ContractIQ.Application.Common.Observability;

/// <summary>
/// Defines the application-owned telemetry vocabulary. Dimensions deliberately
/// exclude prompts, document text, identifiers, secrets, and other customer data.
/// </summary>
public static class ContractIqTelemetry
{
    public const string ActivitySourceName = "ContractIQ";
    public const string MeterName = "ContractIQ";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> AssistantRequests = Meter.CreateCounter<long>(
        "contractiq.assistant.requests",
        description: "Number of contract assistant requests.");
    private static readonly Histogram<double> AssistantDuration = Meter.CreateHistogram<double>(
        "contractiq.assistant.request.duration",
        unit: "s",
        description: "End-to-end contract assistant request duration.");
    private static readonly Histogram<long> AssistantEvidenceCount = Meter.CreateHistogram<long>(
        "contractiq.assistant.evidence.count",
        unit: "{item}",
        description: "Evidence items retrieved for an assistant request.");
    private static readonly Histogram<long> AssistantCitationCount = Meter.CreateHistogram<long>(
        "contractiq.assistant.citation.count",
        unit: "{item}",
        description: "Citations returned by an assistant request.");

    private static readonly Counter<long> KnowledgeSearches = Meter.CreateCounter<long>(
        "contractiq.knowledge.searches",
        description: "Number of hybrid knowledge searches.");
    private static readonly Histogram<double> KnowledgeSearchDuration = Meter.CreateHistogram<double>(
        "contractiq.knowledge.search.duration",
        unit: "s",
        description: "Hybrid knowledge search duration.");
    private static readonly Histogram<long> KnowledgeSearchResultCount = Meter.CreateHistogram<long>(
        "contractiq.knowledge.search.result.count",
        unit: "{item}",
        description: "Evidence items returned by hybrid search.");

    private static readonly Counter<long> ModelRequests = Meter.CreateCounter<long>(
        "contractiq.ai.model.requests",
        description: "Number of assistant model requests.");
    private static readonly Histogram<double> ModelRequestDuration = Meter.CreateHistogram<double>(
        "contractiq.ai.model.request.duration",
        unit: "s",
        description: "Assistant model request duration, including model-selected read tools.");
    private static readonly Counter<long> ModelTokens = Meter.CreateCounter<long>(
        "contractiq.ai.model.tokens",
        unit: "{token}",
        description: "Token usage reported by the configured model provider.");
    private static readonly Counter<long> EmbeddingRequests = Meter.CreateCounter<long>(
        "contractiq.ai.embedding.requests",
        description: "Number of embedding-provider requests.");
    private static readonly Histogram<double> EmbeddingRequestDuration = Meter.CreateHistogram<double>(
        "contractiq.ai.embedding.request.duration",
        unit: "s",
        description: "Embedding-provider request duration.");

    private static readonly Counter<long> ToolCalls = Meter.CreateCounter<long>(
        "contractiq.assistant.tool.calls",
        description: "Number of application tool outcomes.");
    private static readonly Counter<long> CancellationCommands = Meter.CreateCounter<long>(
        "contractiq.cancellation.commands",
        description: "Number of cancellation command outcomes.");
    private static readonly Histogram<double> CancellationCommandDuration = Meter.CreateHistogram<double>(
        "contractiq.cancellation.command.duration",
        unit: "s",
        description: "Cancellation command duration.");

    public static Activity? StartActivity(string name) =>
        ActivitySource.StartActivity(name, ActivityKind.Internal);

    public static void MarkError(Activity? activity, Exception exception)
    {
        activity?.SetTag("error.type", exception.GetType().Name);
        activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
    }

    public static void RecordAssistantRequest(
        string outcome,
        TimeSpan duration,
        int evidenceCount,
        int citationCount)
    {
        var tags = new TagList
        {
            { "contractiq.outcome", outcome },
        };

        AssistantRequests.Add(1, tags);
        AssistantDuration.Record(duration.TotalSeconds, tags);
        AssistantEvidenceCount.Record(evidenceCount, tags);
        AssistantCitationCount.Record(citationCount, tags);
    }

    public static void RecordKnowledgeSearch(
        string outcome,
        TimeSpan duration,
        int resultCount)
    {
        var tags = new TagList
        {
            { "contractiq.outcome", outcome },
        };

        KnowledgeSearches.Add(1, tags);
        KnowledgeSearchDuration.Record(duration.TotalSeconds, tags);
        KnowledgeSearchResultCount.Record(resultCount, tags);
    }

    public static void RecordModelRequest(
        string provider,
        string model,
        string outcome,
        TimeSpan duration,
        long? inputTokens = null,
        long? outputTokens = null,
        long? totalTokens = null)
    {
        var tags = new TagList
        {
            { "gen_ai.provider.name", provider },
            { "gen_ai.request.model", model },
            { "contractiq.outcome", outcome },
        };

        ModelRequests.Add(1, tags);
        ModelRequestDuration.Record(duration.TotalSeconds, tags);
        RecordTokens(inputTokens, "input", provider, model, outcome);
        RecordTokens(outputTokens, "output", provider, model, outcome);
        RecordTokens(totalTokens, "total", provider, model, outcome);
    }

    public static void RecordToolCall(
        string toolName,
        string outcome,
        bool stateChanging)
    {
        var tags = new TagList
        {
            { "gen_ai.tool.name", toolName },
            { "contractiq.outcome", outcome },
            { "contractiq.tool.state_changing", stateChanging },
        };

        ToolCalls.Add(1, tags);
    }

    public static void RecordEmbeddingRequest(
        string provider,
        string model,
        string outcome,
        TimeSpan duration)
    {
        var tags = new TagList
        {
            { "gen_ai.operation.name", "embeddings" },
            { "gen_ai.provider.name", provider },
            { "gen_ai.request.model", model },
            { "contractiq.outcome", outcome },
        };

        EmbeddingRequests.Add(1, tags);
        EmbeddingRequestDuration.Record(duration.TotalSeconds, tags);
    }

    public static void RecordCancellationCommand(
        string outcome,
        bool isReplay,
        TimeSpan duration)
    {
        var tags = new TagList
        {
            { "contractiq.outcome", outcome },
            { "contractiq.command.is_replay", isReplay },
        };

        CancellationCommands.Add(1, tags);
        CancellationCommandDuration.Record(duration.TotalSeconds, tags);
    }

    private static void RecordTokens(
        long? count,
        string tokenType,
        string provider,
        string model,
        string outcome)
    {
        if (count is not > 0)
        {
            return;
        }

        var tags = new TagList
        {
            { "gen_ai.provider.name", provider },
            { "gen_ai.request.model", model },
            { "contractiq.outcome", outcome },
            { "gen_ai.token.type", tokenType },
        };

        ModelTokens.Add(count.Value, tags);
    }
}
