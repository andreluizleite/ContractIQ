namespace ContractIQ.Infrastructure.Persistence.Models;

internal sealed class ContractRecord
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public DateOnly StartDate { get; set; }

    public decimal MonthlyFeeAmount { get; set; }

    public string MonthlyFeeCurrency { get; set; } = string.Empty;

    public int NoticePeriodDays { get; set; }

    public DateOnly MinimumCommitmentEndDate { get; set; }

    public decimal EarlyTerminationPenaltyRate { get; set; }

    public int Status { get; set; }
}
