using ContractIQ.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContractIQ.Infrastructure.Persistence.Configurations;

internal sealed class ContractRecordConfiguration : IEntityTypeConfiguration<ContractRecord>
{
    public void Configure(EntityTypeBuilder<ContractRecord> builder)
    {
        builder.ToTable(
            "contracts",
            table =>
            {
                table.HasCheckConstraint("ck_contracts_monthly_fee_non_negative", "monthly_fee_amount >= 0");
                table.HasCheckConstraint("ck_contracts_notice_period_non_negative", "notice_period_days >= 0");
                table.HasCheckConstraint(
                    "ck_contracts_penalty_rate",
                    "early_termination_penalty_rate >= 0 AND early_termination_penalty_rate <= 1");
                table.HasCheckConstraint(
                    "ck_contracts_commitment_dates",
                    "minimum_commitment_end_date >= start_date");
                table.HasCheckConstraint(
                    "ck_contracts_currency",
                    "monthly_fee_currency ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint("ck_contracts_status", "status IN (1, 2, 3)");
            });

        builder.HasKey(contract => contract.Id).HasName("pk_contracts");

        builder.Property(contract => contract.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(contract => contract.CustomerId)
            .HasColumnName("customer_id");

        builder.Property(contract => contract.StartDate)
            .HasColumnName("start_date")
            .HasColumnType("date");

        builder.Property(contract => contract.MonthlyFeeAmount)
            .HasColumnName("monthly_fee_amount")
            .HasPrecision(18, 2);

        builder.Property(contract => contract.MonthlyFeeCurrency)
            .HasColumnName("monthly_fee_currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(contract => contract.NoticePeriodDays)
            .HasColumnName("notice_period_days");

        builder.Property(contract => contract.MinimumCommitmentEndDate)
            .HasColumnName("minimum_commitment_end_date")
            .HasColumnType("date");

        builder.Property(contract => contract.EarlyTerminationPenaltyRate)
            .HasColumnName("early_termination_penalty_rate")
            .HasPrecision(5, 4);

        builder.Property(contract => contract.Status)
            .HasColumnName("status");

        builder.HasOne<CustomerRecord>()
            .WithMany()
            .HasForeignKey(contract => contract.CustomerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_contracts_customers_customer_id");

        builder.HasIndex(contract => contract.CustomerId)
            .HasDatabaseName("ix_contracts_customer_id");
    }
}
