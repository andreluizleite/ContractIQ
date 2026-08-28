using ContractIQ.Domain.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ContractIQ.Infrastructure.Persistence;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(
        ContractIqDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await SeedCustomerAsync(
            dbContext,
            DemoDataIds.AcmeCustomer,
            "ACME Corporation",
            cancellationToken);
        await SeedCustomerAsync(
            dbContext,
            DemoDataIds.GlobexCustomer,
            "Globex Corporation",
            cancellationToken);
        await SeedCustomerAsync(
            dbContext,
            DemoDataIds.InitechCustomer,
            "Initech",
            cancellationToken);

        await SeedContractAsync(
            dbContext,
            DemoDataIds.AcmeActiveContract,
            DemoDataIds.AcmeCustomer,
            new DateOnly(2026, 1, 1),
            1_200m,
            "USD",
            noticePeriodDays: 30,
            new DateOnly(2028, 1, 1),
            0.25m,
            ContractStatus.Active,
            cancellationToken);
        await SeedContractAsync(
            dbContext,
            DemoDataIds.GlobexActiveContract,
            DemoDataIds.GlobexCustomer,
            new DateOnly(2024, 1, 1),
            850m,
            "USD",
            noticePeriodDays: 15,
            new DateOnly(2025, 1, 1),
            0.20m,
            ContractStatus.Active,
            cancellationToken);
        await SeedContractAsync(
            dbContext,
            DemoDataIds.InitechCancelledContract,
            DemoDataIds.InitechCustomer,
            new DateOnly(2025, 1, 1),
            2_000m,
            "USD",
            noticePeriodDays: 60,
            new DateOnly(2027, 1, 1),
            0.30m,
            ContractStatus.Cancelled,
            cancellationToken);
        await SeedContractAsync(
            dbContext,
            DemoDataIds.InitechExpiredContract,
            DemoDataIds.InitechCustomer,
            new DateOnly(2023, 1, 1),
            1_500m,
            "USD",
            noticePeriodDays: 30,
            new DateOnly(2024, 1, 1),
            0.15m,
            ContractStatus.Expired,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static Task<int> SeedCustomerAsync(
        ContractIqDbContext dbContext,
        Guid id,
        string name,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO customers (id, name)
            VALUES ({id}, {name})
            ON CONFLICT (id) DO NOTHING;
            """,
            cancellationToken);

    private static Task<int> SeedContractAsync(
        ContractIqDbContext dbContext,
        Guid id,
        Guid customerId,
        DateOnly startDate,
        decimal monthlyFeeAmount,
        string monthlyFeeCurrency,
        int noticePeriodDays,
        DateOnly minimumCommitmentEndDate,
        decimal earlyTerminationPenaltyRate,
        ContractStatus status,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO contracts (
                id,
                customer_id,
                start_date,
                monthly_fee_amount,
                monthly_fee_currency,
                notice_period_days,
                minimum_commitment_end_date,
                early_termination_penalty_rate,
                status)
            VALUES (
                {id},
                {customerId},
                {startDate},
                {monthlyFeeAmount},
                {monthlyFeeCurrency},
                {noticePeriodDays},
                {minimumCommitmentEndDate},
                {earlyTerminationPenaltyRate},
                {(int)status})
            ON CONFLICT (id) DO NOTHING;
            """,
            cancellationToken);
}
