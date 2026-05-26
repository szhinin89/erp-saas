using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiCompanyOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_sri_settings",
                table: "sri_settings");

            migrationBuilder.RenameColumn(
                name: "establishment_id",
                table: "warehouse",
                newName: "branch_id");

            migrationBuilder.RenameIndex(
                name: "ix_warehouse_establishment_id",
                table: "warehouse",
                newName: "ix_warehouse_branch_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "sri_settings",
                newName: "id");

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "sri_settings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "branches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_sri_settings",
                table: "sri_settings",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "uq_sri_settings_company_id",
                table: "sri_settings",
                column: "company_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_sri_settings",
                table: "sri_settings");

            migrationBuilder.DropIndex(
                name: "uq_sri_settings_company_id",
                table: "sri_settings");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "sri_settings");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "branches");

            migrationBuilder.RenameColumn(
                name: "branch_id",
                table: "warehouse",
                newName: "establishment_id");

            migrationBuilder.RenameIndex(
                name: "ix_warehouse_branch_id",
                table: "warehouse",
                newName: "ix_warehouse_establishment_id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "sri_settings",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_sri_settings",
                table: "sri_settings",
                column: "subscriber_id");
        }
    }
}
