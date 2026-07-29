using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_method_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    status = table.Column<int>(type: "integer", nullable: false),
                    applied_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    reversed_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    reverse_reason = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    updated_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.CheckConstraint("chk_payments_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "FK_payments_master_business_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_payments_payment_methods_payment_method_id",
                        column: x => x.payment_method_id,
                        principalTable: "payment_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "payment_application_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receivable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    installment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applied_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_application_lines", x => x.id);
                    table.CheckConstraint(
                        "chk_payment_application_line_applied_amount_positive",
                        "applied_amount > 0"
                    );
                    table.CheckConstraint(
                        "chk_payment_application_line_document_xor",
                        "(receivable_id IS NOT NULL AND payable_id IS NULL) OR (receivable_id IS NULL AND payable_id IS NOT NULL)"
                    );
                    table.ForeignKey(
                        name: "FK_payment_application_lines_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_payment_application_lines_purchase_payables_payable_id",
                        column: x => x.payable_id,
                        principalTable: "purchase_payables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_payment_application_lines_sales_receivables_receivable_id",
                        column: x => x.receivable_id,
                        principalTable: "sales_receivables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_payment_application_lines_payable",
                table: "payment_application_lines",
                column: "payable_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_payment_application_lines_payment",
                table: "payment_application_lines",
                column: "payment_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_payment_application_lines_receivable",
                table: "payment_application_lines",
                column: "receivable_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_payments_partner_id",
                table: "payments",
                column: "partner_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_payments_payment_method_id",
                table: "payments",
                column: "payment_method_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_company",
                table: "payments",
                columns: new[] { "tenant_id", "company_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_company_status",
                table: "payments",
                columns: new[] { "tenant_id", "company_id", "status" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_partner",
                table: "payments",
                columns: new[] { "tenant_id", "partner_id" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "payment_application_lines");

            migrationBuilder.DropTable(name: "payments");
        }
    }
}
