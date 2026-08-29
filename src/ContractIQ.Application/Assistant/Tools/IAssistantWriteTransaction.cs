namespace ContractIQ.Application.Assistant.Tools;

public interface IAssistantWriteTransaction
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
