using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierPaymentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "supplier_payment_sequences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_seq = table.Column<int>(type: "integer", nullable: false),
                    prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payment_sequences", x => x.id);
                    table.CheckConstraint("chk_supplier_payment_sequence_current_seq_positive", "\"current_seq\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "supplier_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    system_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    receipt_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    reversed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reverse_reason = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_payments_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_payments_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_payments_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_payment_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accounts_payable_installment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_applied = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payment_applications", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_payment_applications_accounts_payable_installments~",
                        column: x => x.accounts_payable_installment_id,
                        principalTable: "accounts_payable_installments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_payment_applications_supplier_payments_supplier_pa~",
                        column: x => x.supplier_payment_id,
                        principalTable: "supplier_payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_payment_methods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method_id = table.Column<Guid>(type: "uuid", nullable: false),
                    financial_destination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    reference_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    check_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    check_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payment_methods", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_payment_methods_company_financial_destinations_fin~",
                        column: x => x.financial_destination_id,
                        principalTable: "company_financial_destinations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_payment_methods_payment_methods_payment_method_id",
                        column: x => x.payment_method_id,
                        principalTable: "payment_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_payment_methods_supplier_payments_supplier_payment~",
                        column: x => x.supplier_payment_id,
                        principalTable: "supplier_payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_payment_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_payment_method_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_payment_application_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payment_allocations", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_payment_allocations_supplier_payment_applications_~",
                        column: x => x.supplier_payment_application_line_id,
                        principalTable: "supplier_payment_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_supplier_payment_allocations_supplier_payment_methods_suppl~",
                        column: x => x.supplier_payment_method_line_id,
                        principalTable: "supplier_payment_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_supplier_payment_allocations_supplier_payments_supplier_pay~",
                        column: x => x.supplier_payment_id,
                        principalTable: "supplier_payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_allocations_application_line",
                table: "supplier_payment_allocations",
                column: "supplier_payment_application_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_allocations_method_line",
                table: "supplier_payment_allocations",
                column: "supplier_payment_method_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payment_allocations_supplier_payment_id",
                table: "supplier_payment_allocations",
                column: "supplier_payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_allocations_tenant_payment",
                table: "supplier_payment_allocations",
                columns: new[] { "tenant_id", "supplier_payment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_applications_installment",
                table: "supplier_payment_applications",
                column: "accounts_payable_installment_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payment_applications_supplier_payment_id",
                table: "supplier_payment_applications",
                column: "supplier_payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_applications_tenant_payment",
                table: "supplier_payment_applications",
                columns: new[] { "tenant_id", "supplier_payment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_methods_financial_destination",
                table: "supplier_payment_methods",
                column: "financial_destination_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_methods_payment_method",
                table: "supplier_payment_methods",
                column: "payment_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payment_methods_supplier_payment_id",
                table: "supplier_payment_methods",
                column: "supplier_payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_methods_tenant_payment",
                table: "supplier_payment_methods",
                columns: new[] { "tenant_id", "supplier_payment_id" });

            migrationBuilder.CreateIndex(
                name: "uq_supplier_payment_sequences_tenant_company",
                table: "supplier_payment_sequences",
                columns: new[] { "tenant_id", "company_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_branch_id",
                table: "supplier_payments",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_company_id",
                table: "supplier_payments",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_supplier_id",
                table: "supplier_payments",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payments_tenant_company_status",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payments_tenant_company_supplier_date",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "payment_date" });

            migrationBuilder.CreateIndex(
                name: "uq_supplier_payments_tenant_company_supplier_receipt_number",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "receipt_number" },
                unique: true,
                filter: "\"receipt_number\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_supplier_payments_tenant_company_system_number",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "company_id", "system_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_payment_allocations");

            migrationBuilder.DropTable(
                name: "supplier_payment_sequences");

            migrationBuilder.DropTable(
                name: "supplier_payment_applications");

            migrationBuilder.DropTable(
                name: "supplier_payment_methods");

            migrationBuilder.DropTable(
                name: "supplier_payments");
        }
    }
}
