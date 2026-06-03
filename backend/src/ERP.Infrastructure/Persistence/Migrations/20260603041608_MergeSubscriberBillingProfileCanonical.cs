using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MergeSubscriberBillingProfileCanonical : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // STEP 1: Create canonical table first
            migrationBuilder.CreateTable(
                name: "subscriber_billing_profile",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identification_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    identification_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    requires_accounting = table.Column<bool>(type: "boolean", nullable: false),
                    special_taxpayer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    logo_base64 = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    footer_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    receipt_width = table.Column<int>(type: "integer", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriber_billing_profile", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_subscriber_billing_profile_subscriber",
                table: "subscriber_billing_profile",
                column: "subscriber_id",
                unique: true);

            // STEP 2: Migrate data from billing_settings (source of truth — actively used)
            migrationBuilder.Sql("""
                INSERT INTO subscriber_billing_profile (
                    id, subscriber_id,
                    identification_type, identification_number,
                    legal_name, trade_name, address, phone, email,
                    country, city,
                    requires_accounting, special_taxpayer,
                    logo_base64, footer_text, receipt_width,
                    business_partner_id,
                    created_at, updated_at, created_by, updated_by
                )
                SELECT
                    id, subscriber_id,
                    '04' AS identification_type,
                    ruc   AS identification_number,
                    legal_name, trade_name, main_address AS address,
                    NULLIF(phone, '') AS phone,
                    email,
                    'ECU' AS country,
                    NULL  AS city,
                    requires_accounting, special_taxpayer,
                    logo_base64, footer_text, receipt_width,
                    NULL  AS business_partner_id,
                    created_at, updated_at, created_by, updated_by
                FROM billing_settings
                ON CONFLICT (subscriber_id) DO NOTHING;
                """);

            // STEP 3: Drop obsolete tables
            migrationBuilder.DropTable(name: "billing_settings");
            migrationBuilder.DropTable(name: "subscriber_billing_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "subscriber_billing_profile");

            migrationBuilder.CreateTable(
                name: "billing_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    main_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requires_accounting = table.Column<bool>(type: "boolean", nullable: false),
                    special_taxpayer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    logo_base64 = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    footer_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    receipt_width = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table => { table.PrimaryKey("PK_billing_settings", x => x.id); });

            migrationBuilder.CreateIndex("uq_billing_settings_subscriber", "billing_settings", "subscriber_id", unique: true);

            migrationBuilder.CreateTable(
                name: "subscriber_billing_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identification_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    identification_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table => { table.PrimaryKey("PK_subscriber_billing_profiles", x => x.id); });

            migrationBuilder.CreateIndex("ux_subscriber_billing_profiles_subscriber", "subscriber_billing_profiles", "subscriber_id", unique: true);
        }
    }
}
