using ContractIQ.Application.Assistant.Tools;
using ContractIQ.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace ContractIQ.Infrastructure.Assistant;

internal sealed class EfAssistantWriteTransaction(ContractIqDbContext dbContext)
    : IAssistantWriteTransaction
{
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            T result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
