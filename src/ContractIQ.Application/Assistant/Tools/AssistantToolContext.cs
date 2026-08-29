namespace ContractIQ.Application.Assistant.Tools;

public sealed record AssistantToolContext(
    string Question,
    Guid CustomerId,
    Guid ContractId,
    string Language,
    DateOnly AsOf);
