namespace ContractIQ.Application.Assistant;

public sealed record AskContractQuestionCommand(
    string Question,
    Guid CustomerId,
    Guid ContractId,
    string Language);
