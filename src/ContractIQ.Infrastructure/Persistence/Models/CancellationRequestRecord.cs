namespace ContractIQ.Infrastructure.Persistence.Models;

internal sealed class CancellationRequestRecord
{
    public Guid Id { get; set; }

    public Guid ContractId { get; set; }

    public Guid CustomerId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateOnly RequestedOn { get; set; }

    public DateOnly EarliestTerminationDate { get; set; }

    public decimal PenaltyAmount { get; set; }

    public string PenaltyCurrency { get; set; } = string.Empty;

    public int Status { get; set; }
}
