namespace ContractIQ.Application.Assistant.Tools;

public sealed record AssistantToolAuditEvent(
    Guid EventId,
    string ToolName,
    Guid CustomerId,
    Guid ContractId,
    string Outcome,
    bool StateChanging,
    DateTimeOffset OccurredAtUtc);
