namespace ContractIQ.Infrastructure.Persistence.Models;

internal sealed class CustomerRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
