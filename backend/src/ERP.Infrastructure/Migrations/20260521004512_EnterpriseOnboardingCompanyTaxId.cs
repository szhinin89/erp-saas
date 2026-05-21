using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseOnboardingCompanyTaxId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ruc",
                table: "company",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(13)",
                oldFixedLength: true,
                oldMaxLength: 13);

            migrationBuilder.AddColumn<bool>(
                name: "is_provisional_tax_id",
                table: "company",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "tax_id_status",
                table: "company",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Verified");

            migrationBuilder.Sql(
                """
                UPDATE company
                SET ruc = 'TMP-EC-' || substr(replace(id::text, '-', ''), 1, 8),
                    is_provisional_tax_id = true,
                    tax_id_status = 'Pending'
                WHERE ruc = '0000000000000';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_provisional_tax_id",
                table: "company");

            migrationBuilder.DropColumn(
                name: "tax_id_status",
                table: "company");

            migrationBuilder.AlterColumn<string>(
                name: "ruc",
                table: "company",
                type: "character(13)",
                fixedLength: true,
                maxLength: 13,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);
        }
    }
}
