using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigrateBpIdentificationTypeToSriCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convierte valores legacy a códigos SRI ANTES del ALTER COLUMN (varchar 20 → 5)
            migrationBuilder.Sql("""
                UPDATE master_business_partners
                SET identification_type = CASE identification_type
                    WHEN 'RUC'      THEN '04'
                    WHEN 'CI'       THEN '05'
                    WHEN 'PASSPORT' THEN '06'
                    WHEN 'OTHER'    THEN '07'
                    ELSE identification_type
                END
                WHERE identification_type IN ('RUC','CI','PASSPORT','OTHER');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "identification_type",
                table: "master_business_partners",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "identification_type",
                table: "master_business_partners",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5);

            // Revierte códigos SRI a valores legacy
            migrationBuilder.Sql("""
                UPDATE master_business_partners
                SET identification_type = CASE identification_type
                    WHEN '04' THEN 'RUC'
                    WHEN '05' THEN 'CI'
                    WHEN '06' THEN 'PASSPORT'
                    WHEN '07' THEN 'OTHER'
                    ELSE identification_type
                END
                WHERE identification_type IN ('04','05','06','07','08','09');
                """);
        }
    }
}
