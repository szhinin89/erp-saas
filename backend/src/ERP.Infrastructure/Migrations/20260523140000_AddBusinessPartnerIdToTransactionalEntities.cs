using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessPartnerIdToTransactionalEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // customers — adapter link legacy → BP
            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "customers",
                type: "uuid",
                nullable: true);

            // supplier — adapter link legacy → BP
            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "supplier",
                type: "uuid",
                nullable: true);

            // purch_bill
            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "purch_bill",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_purch_bill_business_partner_id",
                table: "purch_bill",
                columns: new[] { "subscriber_id", "business_partner_id" },
                filter: "business_partner_id IS NOT NULL");

            // sales_bill
            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "sales_bill",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_bill_business_partner_id",
                table: "sales_bill",
                columns: new[] { "subscriber_id", "business_partner_id" },
                filter: "business_partner_id IS NOT NULL");

            // sales_document
            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "sales_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_document_business_partner_id",
                table: "sales_document",
                columns: new[] { "subscriber_id", "business_partner_id" },
                filter: "business_partner_id IS NOT NULL");

            // purchase_document
            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "purchase_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_document_business_partner_id",
                table: "purchase_document",
                columns: new[] { "subscriber_id", "business_partner_id" },
                filter: "business_partner_id IS NOT NULL");

            // purchase_order
            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "purchase_order",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_business_partner_id",
                table: "purchase_order",
                columns: new[] { "subscriber_id", "business_partner_id" },
                filter: "business_partner_id IS NOT NULL");

            // expense_document
            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "expense_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_document_business_partner_id",
                table: "expense_document",
                columns: new[] { "subscriber_id", "business_partner_id" },
                filter: "business_partner_id IS NOT NULL");

            // purchase_invoice
            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "purchase_invoice",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_business_partner_id",
                table: "purchase_invoice",
                columns: new[] { "company_id", "business_partner_id" },
                filter: "business_partner_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("business_partner_id", "customers");
            migrationBuilder.DropColumn("business_partner_id", "supplier");

            migrationBuilder.DropIndex("ix_purch_bill_business_partner_id", "purch_bill");
            migrationBuilder.DropColumn("business_partner_id", "purch_bill");

            migrationBuilder.DropIndex("ix_sales_bill_business_partner_id", "sales_bill");
            migrationBuilder.DropColumn("business_partner_id", "sales_bill");

            migrationBuilder.DropIndex("ix_sales_document_business_partner_id", "sales_document");
            migrationBuilder.DropColumn("business_partner_id", "sales_document");

            migrationBuilder.DropIndex("ix_purchase_document_business_partner_id", "purchase_document");
            migrationBuilder.DropColumn("business_partner_id", "purchase_document");

            migrationBuilder.DropIndex("ix_purchase_order_business_partner_id", "purchase_order");
            migrationBuilder.DropColumn("business_partner_id", "purchase_order");

            migrationBuilder.DropIndex("ix_expense_document_business_partner_id", "expense_document");
            migrationBuilder.DropColumn("business_partner_id", "expense_document");

            migrationBuilder.DropIndex("ix_purchase_invoice_business_partner_id", "purchase_invoice");
            migrationBuilder.DropColumn("business_partner_id", "purchase_invoice");
        }
    }
}
