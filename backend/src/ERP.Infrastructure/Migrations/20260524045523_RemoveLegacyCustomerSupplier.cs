using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyCustomerSupplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_issued_retention_supplier_supplier_id",
                table: "issued_retention");

            migrationBuilder.DropForeignKey(
                name: "FK_purch_note_supplier_supplier_id",
                table: "purch_note");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_withholding_supplier_supplier_id",
                table: "purchase_withholding");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_bill_customers_customer_id",
                table: "sales_bill");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_document_customers_customer_id",
                table: "sales_document");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_retention_customers_customer_id",
                table: "sales_retention");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_withholding_customers_customer_id",
                table: "sales_withholding");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "supplier");

            migrationBuilder.DropIndex(
                name: "IX_sales_document_customer_id",
                table: "sales_document");

            migrationBuilder.DropIndex(
                name: "ix_sales_document_subscriber_customer_date",
                table: "sales_document");

            migrationBuilder.DropIndex(
                name: "IX_sales_bill_customer_id",
                table: "sales_bill");

            migrationBuilder.DropIndex(
                name: "ix_purchase_order_subscriber_supplier",
                table: "purchase_order");

            migrationBuilder.DropIndex(
                name: "idx_pi_supplier",
                table: "purchase_invoice");

            migrationBuilder.DropIndex(
                name: "uq_pi_number",
                table: "purchase_invoice");

            migrationBuilder.DropIndex(
                name: "ix_purch_bill_subscriber_supplier_status",
                table: "purch_bill");

            migrationBuilder.DropIndex(
                name: "uq_purch_bill_supplier_invoice",
                table: "purch_bill");

            migrationBuilder.DropColumn(
                name: "customer_id",
                table: "sales_document");

            migrationBuilder.DropColumn(
                name: "customer_id",
                table: "sales_bill");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                table: "purchase_document");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                table: "purch_bill");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                table: "expense_document");

            migrationBuilder.DropColumn(
                name: "customer_id",
                table: "ar_entries");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                table: "ap_entries");

            migrationBuilder.RenameColumn(
                name: "supplier_id",
                table: "withholding_cert",
                newName: "business_partner_id");

            migrationBuilder.RenameColumn(
                name: "customer_id",
                table: "vat_refund",
                newName: "business_partner_id");

            migrationBuilder.RenameColumn(
                name: "supplier_id",
                table: "supplier_note",
                newName: "business_partner_id");

            migrationBuilder.RenameColumn(
                name: "customer_id",
                table: "sales_withholding",
                newName: "business_partner_id");

            migrationBuilder.RenameIndex(
                name: "IX_sales_withholding_customer_id",
                table: "sales_withholding",
                newName: "IX_sales_withholding_business_partner_id");

            migrationBuilder.RenameColumn(
                name: "customer_id",
                table: "sales_retention",
                newName: "business_partner_id");

            migrationBuilder.RenameIndex(
                name: "IX_sales_retention_customer_id",
                table: "sales_retention",
                newName: "IX_sales_retention_business_partner_id");

            migrationBuilder.RenameColumn(
                name: "customer_id",
                table: "sales_invoice",
                newName: "business_partner_id");

            migrationBuilder.RenameColumn(
                name: "customer_id",
                table: "received_withholding",
                newName: "business_partner_id");

            migrationBuilder.RenameColumn(
                name: "supplier_id",
                table: "purchase_withholding",
                newName: "business_partner_id");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_withholding_supplier_id",
                table: "purchase_withholding",
                newName: "IX_purchase_withholding_business_partner_id");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                table: "purchase_invoice");

            migrationBuilder.RenameColumn(
                name: "supplier_id",
                table: "purch_note",
                newName: "business_partner_id");

            migrationBuilder.RenameIndex(
                name: "IX_purch_note_supplier_id",
                table: "purch_note",
                newName: "IX_purch_note_business_partner_id");

            migrationBuilder.RenameColumn(
                name: "supplier_id",
                table: "product_supplier_codes",
                newName: "business_partner_id");

            migrationBuilder.RenameColumn(
                name: "supplier_id",
                table: "issued_retention",
                newName: "business_partner_id");

            migrationBuilder.RenameIndex(
                name: "IX_issued_retention_supplier_id",
                table: "issued_retention",
                newName: "IX_issued_retention_business_partner_id");

            migrationBuilder.RenameColumn(
                name: "supplier_id",
                table: "expense_invoice",
                newName: "business_partner_id");

            migrationBuilder.RenameColumn(
                name: "customer_id",
                table: "debit_note",
                newName: "business_partner_id");

            migrationBuilder.RenameColumn(
                name: "customer_id",
                table: "credit_note",
                newName: "business_partner_id");

            migrationBuilder.AlterColumn<Guid>(
                name: "business_partner_id",
                table: "sales_bill",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "business_partner_id",
                table: "purchase_order",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "business_partner_id",
                table: "purchase_invoice",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "business_partner_id",
                table: "purch_bill",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "business_partner_id",
                table: "ar_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "business_partner_id",
                table: "ap_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_document_business_partner_id",
                table: "sales_document",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_document_subscriber_customer_date",
                table: "sales_document",
                columns: new[] { "subscriber_id", "business_partner_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_bill_business_partner_id",
                table: "sales_bill",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_subscriber_supplier",
                table: "purchase_order",
                columns: new[] { "subscriber_id", "business_partner_id" });

            migrationBuilder.CreateIndex(
                name: "idx_pi_supplier",
                table: "purchase_invoice",
                columns: new[] { "company_id", "business_partner_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_pi_number",
                table: "purchase_invoice",
                columns: new[] { "company_id", "business_partner_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purch_bill_subscriber_supplier_status",
                table: "purch_bill",
                columns: new[] { "subscriber_id", "business_partner_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_purch_bill_supplier_invoice",
                table: "purch_bill",
                columns: new[] { "subscriber_id", "business_partner_id", "invoice_number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_issued_retention_master_business_partners_business_partner_~",
                table: "issued_retention",
                column: "business_partner_id",
                principalTable: "master_business_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purch_note_master_business_partners_business_partner_id",
                table: "purch_note",
                column: "business_partner_id",
                principalTable: "master_business_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_withholding_master_business_partners_business_part~",
                table: "purchase_withholding",
                column: "business_partner_id",
                principalTable: "master_business_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_bill_master_business_partners_business_partner_id",
                table: "sales_bill",
                column: "business_partner_id",
                principalTable: "master_business_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_document_master_business_partners_business_partner_id",
                table: "sales_document",
                column: "business_partner_id",
                principalTable: "master_business_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_retention_master_business_partners_business_partner_id",
                table: "sales_retention",
                column: "business_partner_id",
                principalTable: "master_business_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_withholding_master_business_partners_business_partner~",
                table: "sales_withholding",
                column: "business_partner_id",
                principalTable: "master_business_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_issued_retention_master_business_partners_business_partner_~",
                table: "issued_retention");

            migrationBuilder.DropForeignKey(
                name: "FK_purch_note_master_business_partners_business_partner_id",
                table: "purch_note");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_withholding_master_business_partners_business_part~",
                table: "purchase_withholding");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_bill_master_business_partners_business_partner_id",
                table: "sales_bill");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_document_master_business_partners_business_partner_id",
                table: "sales_document");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_retention_master_business_partners_business_partner_id",
                table: "sales_retention");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_withholding_master_business_partners_business_partner~",
                table: "sales_withholding");

            migrationBuilder.DropIndex(
                name: "IX_sales_document_business_partner_id",
                table: "sales_document");

            migrationBuilder.DropIndex(
                name: "ix_sales_document_subscriber_customer_date",
                table: "sales_document");

            migrationBuilder.DropIndex(
                name: "IX_sales_bill_business_partner_id",
                table: "sales_bill");

            migrationBuilder.DropIndex(
                name: "ix_purchase_order_subscriber_supplier",
                table: "purchase_order");

            migrationBuilder.DropIndex(
                name: "idx_pi_supplier",
                table: "purchase_invoice");

            migrationBuilder.DropIndex(
                name: "uq_pi_number",
                table: "purchase_invoice");

            migrationBuilder.DropIndex(
                name: "ix_purch_bill_subscriber_supplier_status",
                table: "purch_bill");

            migrationBuilder.DropIndex(
                name: "uq_purch_bill_supplier_invoice",
                table: "purch_bill");

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "withholding_cert",
                newName: "supplier_id");

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "vat_refund",
                newName: "customer_id");

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "supplier_note",
                newName: "supplier_id");

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "sales_withholding",
                newName: "customer_id");

            migrationBuilder.RenameIndex(
                name: "IX_sales_withholding_business_partner_id",
                table: "sales_withholding",
                newName: "IX_sales_withholding_customer_id");

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "sales_retention",
                newName: "customer_id");

            migrationBuilder.RenameIndex(
                name: "IX_sales_retention_business_partner_id",
                table: "sales_retention",
                newName: "IX_sales_retention_customer_id");

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "sales_invoice",
                newName: "customer_id");

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "received_withholding",
                newName: "customer_id");

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "purchase_withholding",
                newName: "supplier_id");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_withholding_business_partner_id",
                table: "purchase_withholding",
                newName: "IX_purchase_withholding_supplier_id");

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                table: "purchase_invoice",
                type: "uuid",
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "purch_note",
                newName: "supplier_id");

            migrationBuilder.RenameIndex(
                name: "IX_purch_note_business_partner_id",
                table: "purch_note",
                newName: "IX_purch_note_supplier_id");

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "product_supplier_codes",
                newName: "supplier_id");

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "issued_retention",
                newName: "supplier_id");

            migrationBuilder.RenameIndex(
                name: "IX_issued_retention_business_partner_id",
                table: "issued_retention",
                newName: "IX_issued_retention_supplier_id");

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "expense_invoice",
                newName: "supplier_id");

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "debit_note",
                newName: "customer_id");

            migrationBuilder.RenameColumn(
                name: "business_partner_id",
                table: "credit_note",
                newName: "customer_id");

            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                table: "sales_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "business_partner_id",
                table: "sales_bill",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                table: "sales_bill",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "business_partner_id",
                table: "purchase_order",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                table: "purchase_order",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "business_partner_id",
                table: "purchase_invoice",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                table: "purchase_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "business_partner_id",
                table: "purch_bill",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                table: "purch_bill",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                table: "expense_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "business_partner_id",
                table: "ar_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                table: "ar_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "business_partner_id",
                table: "ap_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                table: "ap_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    address_line = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    country_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    identification_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    identification_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    payment_days = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    country_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payment_days = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    payment_terms = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    person_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_support_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_document_customer_id",
                table: "sales_document",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_document_subscriber_customer_date",
                table: "sales_document",
                columns: new[] { "subscriber_id", "customer_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_bill_customer_id",
                table: "sales_bill",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_subscriber_supplier",
                table: "purchase_order",
                columns: new[] { "subscriber_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "idx_pi_supplier",
                table: "purchase_invoice",
                columns: new[] { "company_id", "supplier_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_pi_number",
                table: "purchase_invoice",
                columns: new[] { "company_id", "supplier_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purch_bill_subscriber_supplier_status",
                table: "purch_bill",
                columns: new[] { "subscriber_id", "supplier_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_purch_bill_supplier_invoice",
                table: "purch_bill",
                columns: new[] { "subscriber_id", "supplier_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_subscriber_company",
                table: "customers",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ux_customers_subscriber_company_doc",
                table: "customers",
                columns: new[] { "subscriber_id", "company_id", "identification_type", "identification_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_subscriber_id",
                table: "supplier",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "uq_supplier_ruc",
                table: "supplier",
                columns: new[] { "subscriber_id", "ruc" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_issued_retention_supplier_supplier_id",
                table: "issued_retention",
                column: "supplier_id",
                principalTable: "supplier",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purch_note_supplier_supplier_id",
                table: "purch_note",
                column: "supplier_id",
                principalTable: "supplier",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_withholding_supplier_supplier_id",
                table: "purchase_withholding",
                column: "supplier_id",
                principalTable: "supplier",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_bill_customers_customer_id",
                table: "sales_bill",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_document_customers_customer_id",
                table: "sales_document",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_retention_customers_customer_id",
                table: "sales_retention",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_withholding_customers_customer_id",
                table: "sales_withholding",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
