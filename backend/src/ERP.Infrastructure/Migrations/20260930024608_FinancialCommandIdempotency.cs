using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCommandIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "client_request_id",
                table: "supplier_payments",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "request_payload_hash",
                table: "supplier_payments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "client_request_id",
                table: "payments",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "request_payload_hash",
                table: "payments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "client_request_id",
                table: "cash_movements",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "request_payload_hash",
                table: "cash_movements",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "uq_supplier_payments_tenant_client_request_id",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "client_request_id" },
                unique: true,
                filter: "client_request_id IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "uq_payments_tenant_client_request_id",
                table: "payments",
                columns: new[] { "tenant_id", "client_request_id" },
                unique: true,
                filter: "client_request_id IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "uq_cash_movements_tenant_client_request_id",
                table: "cash_movements",
                columns: new[] { "tenant_id", "client_request_id" },
                unique: true,
                filter: "client_request_id IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_supplier_payments_tenant_client_request_id",
                table: "supplier_payments"
            );

            migrationBuilder.DropIndex(
                name: "uq_payments_tenant_client_request_id",
                table: "payments"
            );

            migrationBuilder.DropIndex(
                name: "uq_cash_movements_tenant_client_request_id",
                table: "cash_movements"
            );

            migrationBuilder.DropColumn(name: "client_request_id", table: "supplier_payments");

            migrationBuilder.DropColumn(name: "request_payload_hash", table: "supplier_payments");

            migrationBuilder.DropColumn(name: "client_request_id", table: "payments");

            migrationBuilder.DropColumn(name: "request_payload_hash", table: "payments");

            migrationBuilder.DropColumn(name: "client_request_id", table: "cash_movements");

            migrationBuilder.DropColumn(name: "request_payload_hash", table: "cash_movements");
        }
    }
}
