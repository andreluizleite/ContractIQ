using ContractIQ.Application.Assistant.Tools;
using ContractIQ.Application.Common.Observability;
using Microsoft.Extensions.Logging;

namespace ContractIQ.Infrastructure.Assistant;

internal sealed class LoggingAssistantToolAudit(
    ILogger<LoggingAssistantToolAudit> logger) : IAssistantToolAudit
{
    public Task RecordAsync(
        AssistantToolAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ContractIqTelemetry.RecordToolCall(
            auditEvent.ToolName,
            auditEvent.Outcome,
            auditEvent.StateChanging);

        logger.LogInformation(
            "Assistant tool outcome: EventId={EventId}, Tool={ToolName}, Outcome={Outcome}, StateChanging={StateChanging}, OccurredAtUtc={OccurredAtUtc}",
            auditEvent.EventId,
            auditEvent.ToolName,
            auditEvent.Outcome,
            auditEvent.StateChanging,
            auditEvent.OccurredAtUtc);

        return Task.CompletedTask;
    }
}
