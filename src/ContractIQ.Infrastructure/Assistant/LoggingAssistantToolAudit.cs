using ContractIQ.Application.Assistant.Tools;
using Microsoft.Extensions.Logging;

namespace ContractIQ.Infrastructure.Assistant;

internal sealed class LoggingAssistantToolAudit(
    ILogger<LoggingAssistantToolAudit> logger) : IAssistantToolAudit
{
    public Task RecordAsync(
        AssistantToolAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Assistant tool outcome: EventId={EventId}, Tool={ToolName}, CustomerId={CustomerId}, ContractId={ContractId}, Outcome={Outcome}, StateChanging={StateChanging}, OccurredAtUtc={OccurredAtUtc}",
            auditEvent.EventId,
            auditEvent.ToolName,
            auditEvent.CustomerId,
            auditEvent.ContractId,
            auditEvent.Outcome,
            auditEvent.StateChanging,
            auditEvent.OccurredAtUtc);

        return Task.CompletedTask;
    }
}
