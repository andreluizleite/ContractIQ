namespace ContractIQ.Application.Assistant.Tools;

public interface IAssistantToolAudit
{
    Task RecordAsync(
        AssistantToolAuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}
