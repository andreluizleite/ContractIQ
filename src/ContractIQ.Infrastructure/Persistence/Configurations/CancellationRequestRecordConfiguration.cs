using ContractIQ.Domain.Cancellations;
using ContractIQ.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContractIQ.Infrastructure.Persistence.Configurations;

internal sealed class CancellationRequestRecordConfiguration :
    IEntityTypeConfiguration<CancellationRequestRecord>
{
    public void Configure(EntityTypeBuilder<CancellationRequestRecord> builder)
    {
        builder.ToTable(
            "cancellation_requests",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_cancellation_requests_penalty_non_negative",
                    "penalty_amount >= 0");
                table.HasCheckConstraint(
                    "ck_cancellation_requests_dates",
                    "earliest_termination_date >= requested_on");
                table.HasCheckConstraint(
                    "ck_cancellation_requests_currency",
                    "penalty_currency ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint(
                    "ck_cancellation_requests_status",
                    "status IN (1, 2, 3)");
            });

        builder.HasKey(request => request.Id).HasName("pk_cancellation_requests");

        builder.Property(request => request.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(request => request.ContractId)
            .HasColumnName("contract_id");

        builder.Property(request => request.CustomerId)
            .HasColumnName("customer_id");

        builder.Property(request => request.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(request => request.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(request => request.RequestedOn)
            .HasColumnName("requested_on")
            .HasColumnType("date");

        builder.Property(request => request.EarliestTerminationDate)
            .HasColumnName("earliest_termination_date")
            .HasColumnType("date");

        builder.Property(request => request.PenaltyAmount)
            .HasColumnName("penalty_amount")
            .HasPrecision(18, 2);

        builder.Property(request => request.PenaltyCurrency)
            .HasColumnName("penalty_currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(request => request.Status)
            .HasColumnName("status");

        builder.HasOne<ContractRecord>()
            .WithMany()
            .HasForeignKey(request => request.ContractId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cancellation_requests_contracts_contract_id");

        builder.HasOne<CustomerRecord>()
            .WithMany()
            .HasForeignKey(request => request.CustomerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cancellation_requests_customers_customer_id");

        builder.HasIndex(request => request.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_cancellation_requests_idempotency_key");

        builder.HasIndex(request => request.ContractId)
            .IsUnique()
            .HasFilter($"status = {(int)CancellationRequestStatus.PendingReview}")
            .HasDatabaseName("ux_cancellation_requests_open_contract");

        builder.HasIndex(request => request.CustomerId)
            .HasDatabaseName("ix_cancellation_requests_customer_id");
    }
}
