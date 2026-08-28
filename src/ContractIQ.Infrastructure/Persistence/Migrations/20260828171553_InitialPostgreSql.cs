using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contracts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    monthly_fee_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    monthly_fee_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    notice_period_days = table.Column<int>(type: "integer", nullable: false),
                    minimum_commitment_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    early_termination_penalty_rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contracts", x => x.id);
                    table.CheckConstraint("ck_contracts_commitment_dates", "minimum_commitment_end_date >= start_date");
                    table.CheckConstraint("ck_contracts_currency", "monthly_fee_currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_contracts_monthly_fee_non_negative", "monthly_fee_amount >= 0");
                    table.CheckConstraint("ck_contracts_notice_period_non_negative", "notice_period_days >= 0");
                    table.CheckConstraint("ck_contracts_penalty_rate", "early_termination_penalty_rate >= 0 AND early_termination_penalty_rate <= 1");
                    table.CheckConstraint("ck_contracts_status", "status IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "fk_contracts_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cancellation_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    requested_on = table.Column<DateOnly>(type: "date", nullable: false),
                    earliest_termination_date = table.Column<DateOnly>(type: "date", nullable: false),
                    penalty_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    penalty_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cancellation_requests", x => x.id);
                    table.CheckConstraint("ck_cancellation_requests_currency", "penalty_currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_cancellation_requests_dates", "earliest_termination_date >= requested_on");
                    table.CheckConstraint("ck_cancellation_requests_penalty_non_negative", "penalty_amount >= 0");
                    table.CheckConstraint("ck_cancellation_requests_status", "status IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "fk_cancellation_requests_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cancellation_requests_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cancellation_requests_customer_id",
                table: "cancellation_requests",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ux_cancellation_requests_idempotency_key",
                table: "cancellation_requests",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_cancellation_requests_open_contract",
                table: "cancellation_requests",
                column: "contract_id",
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ix_contracts_customer_id",
                table: "contracts",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_name",
                table: "customers",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cancellation_requests");

            migrationBuilder.DropTable(
                name: "contracts");

            migrationBuilder.DropTable(
                name: "customers");
        }
    }
}
