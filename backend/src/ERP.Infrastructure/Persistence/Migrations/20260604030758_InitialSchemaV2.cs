using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchemaV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "global");

            migrationBuilder.CreateTable(
                name: "access_profile_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    is_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_profile_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "access_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "accounting_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_periods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    nature = table.Column<string>(type: "text", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    allows_movements = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ap_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    original_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ap_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_features",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    permission = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_visible_in_menu = table.Column<bool>(type: "boolean", nullable: false),
                    is_platform_only_feature = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_features", x => x.id);
                    table.ForeignKey(
                        name: "FK_app_features_app_features_parent_id",
                        column: x => x.parent_id,
                        principalTable: "app_features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ar_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    original_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ar_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "billing_checkout_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<int>(type: "integer", nullable: false),
                    provider_session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    checkout_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    plan_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    commercial_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    billing_cycle = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    linked_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_checkout_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "billing_payment_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<int>(type: "integer", nullable: false),
                    provider_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    requested_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_payment_attempts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "brands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    country_of_origin = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brands", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "carriers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identification_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    identification_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    license_plate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carriers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "commercial_plan_features",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_included = table.Column<bool>(type: "boolean", nullable: false),
                    limit_per_period = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_plan_features", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "commercial_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    short_label = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    billing_cycle = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_publicly_visible = table.Column<bool>(type: "boolean", nullable: false),
                    is_recommended = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    external_billing_ref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    menu_config = table.Column<string>(type: "jsonb", nullable: true),
                    menu_sidebar_layout = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "horizontal")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "config_feature",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    data_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_feature", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "config_global",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    data_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_global", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "config_module",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    data_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_module", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "current_stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    reserved_quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    total_stock_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_current_stock", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_relation",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    target_module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_id = table.Column<long>(type: "bigint", nullable: true),
                    relation_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_relation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expense_document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    doc_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    doc_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    concept = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_notes_applied = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_document", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expense_invoice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    concept = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    validated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_notes_applied = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_invoice", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "first_run_setup_state",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_first_run = table.Column<bool>(type: "boolean", nullable: false),
                    setup_token_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    setup_token_expiry_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_first_run_setup_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "identity_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email_normalized = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    user_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    platform_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: true),
                    security_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    require_password_reset = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoice",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_discount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    payment_method_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    payment_term_days = table.Column<short>(type: "smallint", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    buyer_id_type = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    buyer_id_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    buyer_name_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    buyer_address_snapshot = table.Column<string>(type: "text", nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    accounting_period_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_posted = table.Column<int>(type: "integer", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kardex_report",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    date_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    result_json = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kardex_report", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kardex_snapshot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_date = table.Column<DateTime>(type: "date", nullable: false),
                    balance_qty = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    balance_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    average_cost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    computed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kardex_snapshot", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_business_partners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identification_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    identification_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    person_type = table.Column<short>(type: "smallint", nullable: false),
                    country_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_business_partners", x => x.id);
                    table.UniqueConstraint("uq_mbp_id_subscriber", x => new { x.id, x.subscriber_id });
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SubscriberId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ar_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ap_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    application_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payment_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_applications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_provider_customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<int>(type: "integer", nullable: false),
                    external_customer_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_provider_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_provider_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<int>(type: "integer", nullable: false),
                    external_subscription_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    commercial_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    current_period_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_period_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancel_at_period_end = table.Column<bool>(type: "boolean", nullable: false),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_provider_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    target_subscriber_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resource_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    new_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_features",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_metered = table.Column<bool>(type: "boolean", nullable: false),
                    feature_kind = table.Column<byte>(type: "smallint", nullable: false),
                    resource_ref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_features", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_provisioning_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<int>(type: "integer", nullable: false),
                    timestamp_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    instance_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", maxLength: 8000, nullable: true),
                    is_success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_provisioning_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_provisioning_lock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false),
                    locked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locked_by_instance = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_provisioning_lock", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_webhook_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<int>(type: "integer", nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    raw_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_webhook_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_lines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_subcategories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_subcategories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purch_bill",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    invoice_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payment_terms = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    validated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_notes_applied = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_bill", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purch_warehouse_alloc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_bill_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_warehouse_alloc", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    doc_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_notes_applied = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    payment_terms = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferenceDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_document", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_document_purchase_document_ReferenceDocumentId",
                        column: x => x.ReferenceDocumentId,
                        principalTable: "purchase_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequential = table.Column<int>(type: "integer", nullable: false),
                    order_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    required_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    delivery_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    target_warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_bill",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    linked_by = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_bill", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quote",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    payment_term_days = table.Column<short>(type: "smallint", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "received_withholding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    access_key = table.Column<string>(type: "character(49)", fixedLength: true, maxLength: 49, nullable: true),
                    issuer_ruc = table.Column<string>(type: "character(13)", fixedLength: true, maxLength: 13, nullable: false),
                    issuer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sales_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_received_withholding", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    reason_revoked = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rotation_depth = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "retry_control",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retry_count = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    max_retries = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)5),
                    last_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_exhausted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retry_control", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saas_billing_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_billing_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saas_billing_invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    provider_type = table.Column<int>(type: "integer", nullable: false),
                    external_invoice_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    period_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    due_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paid_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    erp_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_billing_invoices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_note",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    doc_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_note", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_order",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    required_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    payment_term_days = table.Column<short>(type: "smallint", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "security_admin_scope_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    is_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_admin_scope_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sri_country",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    iso2 = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_country", x => x.code);
                    table.UniqueConstraint("AK_sri_country_iso2", x => x.iso2);
                });

            migrationBuilder.CreateTable(
                name: "sri_doc_type",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    short_name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_electronic = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_doc_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_emission_type",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_emission_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_environment",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    abbrev = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_environment", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_error_code",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    error_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_error_code", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_ice_rate",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    unit_value = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_ice_rate", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_id_type",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    digits = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_id_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_payment_method",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_payment_method", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_retention_code",
                schema: "global",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tax_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    applies_to = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, defaultValue: "SUPPLIER"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_retention_code", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sri_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cert_p12_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    cert_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    environment = table.Column<int>(type: "integer", nullable: false),
                    emission_type = table.Column<int>(type: "integer", nullable: false),
                    wsdl_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sri_tax_regime",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    abbrev = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_tax_regime", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_tax_support",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_tax_support", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_uom",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    abbrev = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_uom", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_vat_rate",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_vat_rate", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "stock_adjustment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequential = table.Column<int>(type: "integer", nullable: false),
                    adjustment_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    adjustment_qty = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    adjustment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    adjustment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    executed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_adjustment", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_movement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    previous_quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    result_quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_doc_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    unit_cost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    total_cost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movement", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reservations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscriber_billing_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    renewal_state = table.Column<int>(type: "integer", nullable: false),
                    trial_state = table.Column<int>(type: "integer", nullable: false),
                    primary_provider = table.Column<int>(type: "integer", nullable: false),
                    external_customer_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    billing_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    default_payment_method_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    trial_ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    grace_period_ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_period_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriber_billing_accounts", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "subscriber_subscription_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    previous_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriber_subscription_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscriber_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_period_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriber_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscribers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    lifecycle_status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    password_reset_mode = table.Column<int>(type: "integer", nullable: false),
                    plan_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    suspended_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    suspended_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    preferred_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "es"),
                    is_internal_platform_owner = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscribers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_feature_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    limit_override_per_period = table.Column<long>(type: "bigint", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_feature_overrides", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_usages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_usages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tariffs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tariffs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ui_nav_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    label_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    module_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    roles_csv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    require_platform_panel = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ui_nav_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_activity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    user_full_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_activity", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vat_refund",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    applied_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    sri_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vat_refund", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "warehouse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    storage_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    manager = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    latitude = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    longitude = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    capacity = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    daily_dispatch_goal = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    sri_establishment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ws_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    environment = table.Column<short>(type: "smallint", nullable: false),
                    endpoint_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    request_payload = table.Column<string>(type: "text", nullable: true),
                    response_payload = table.Column<string>(type: "text", nullable: true),
                    http_status = table.Column<short>(type: "smallint", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: true),
                    error_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    error_detail = table.Column<string>(type: "text", nullable: true),
                    called_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ws_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "accounting_setup",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_of_sales_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    suppliers_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sales_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customers_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vat_purchases_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vat_sales_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_setup", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_bank_account_id",
                        column: x => x.bank_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_cash_account_id",
                        column: x => x.cash_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_cost_of_sales_account_id",
                        column: x => x.cost_of_sales_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_customers_account_id",
                        column: x => x.customers_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_inventory_account_id",
                        column: x => x.inventory_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_sales_account_id",
                        column: x => x.sales_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_suppliers_account_id",
                        column: x => x.suppliers_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_vat_purchases_account_id",
                        column: x => x.vat_purchases_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_vat_sales_account_id",
                        column: x => x.vat_sales_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bank_account",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    account_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    initial_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    current_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ledger_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_account", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_account_accounts_ledger_account_id",
                        column: x => x.ledger_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "expense_category",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    expense_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_category", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_category_accounts_expense_account_id",
                        column: x => x.expense_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "commercial_plan_limits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    commercial_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    limit_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    limit_value = table.Column<long>(type: "bigint", nullable: false),
                    period_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_hard_limit = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_plan_limits", x => x.id);
                    table.ForeignKey(
                        name: "FK_commercial_plan_limits_commercial_plans_commercial_plan_id",
                        column: x => x.commercial_plan_id,
                        principalTable: "commercial_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expense_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    line_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_detail_expense_document_expense_id",
                        column: x => x.expense_id,
                        principalTable: "expense_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_expense_detail_expense_invoice_expense_id",
                        column: x => x.expense_id,
                        principalTable: "expense_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_detail",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<short>(type: "smallint", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sku_snapshot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    unit_name_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_price_snapshot = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_code_snapshot = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    tax_rate_snapshot = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    line_subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_tax = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_detail_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_electronic",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    authorization_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    authorization_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_authorized_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_electronic", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_electronic_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_status_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_status_history_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journal_entry_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    debit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    debit_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    credit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    credit_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entry_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "master_bp_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_type = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_bp_roles", x => x.id);
                    table.UniqueConstraint("uq_bpr_id_subscriber", x => new { x.id, x.subscriber_id });
                    table.ForeignKey(
                        name: "fk_bpr_business_partner",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "master_company_bp_trading_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    credit_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false, defaultValue: "USD"),
                    payment_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    blocked_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    blocked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    blocked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_company_bp_trading_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_cbts_business_partner",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "issued_retention",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    voucher_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    establishment_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    emission_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issued_retention", x => x.id);
                    table.ForeignKey(
                        name: "FK_issued_retention_master_business_partners_business_partner_~",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issued_retention_purch_bill_purch_bill_id",
                        column: x => x.purch_bill_id,
                        principalTable: "purch_bill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "purch_bill_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    supplier_product_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_bill_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_purch_bill_line_purch_bill_purch_bill_id",
                        column: x => x.purch_bill_id,
                        principalTable: "purch_bill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purch_note",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expense_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_note", x => x.id);
                    table.ForeignKey(
                        name: "FK_purch_note_expense_invoice_expense_invoice_id",
                        column: x => x.expense_invoice_id,
                        principalTable: "expense_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purch_note_master_business_partners_business_partner_id",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purch_note_purch_bill_purch_bill_id",
                        column: x => x.purch_bill_id,
                        principalTable: "purch_bill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    qty_received = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountPct = table.Column<decimal>(type: "numeric", nullable: false),
                    VatCode = table.Column<string>(type: "text", nullable: false),
                    VatPercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_detail_purchase_document_purchase_document_id",
                        column: x => x.purchase_document_id,
                        principalTable: "purchase_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_electronic_doc",
                columns: table => new
                {
                    purchase_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_electronic_doc", x => x.purchase_document_id);
                    table.ForeignKey(
                        name: "FK_purchase_electronic_doc_purchase_document_purchase_document~",
                        column: x => x.purchase_document_id,
                        principalTable: "purchase_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_withholding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    purchase_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    voucher_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    total_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_withholding", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_withholding_master_business_partners_business_part~",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_withholding_purchase_document_purchase_document_id",
                        column: x => x.purchase_document_id,
                        principalTable: "purchase_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ordered_qty = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    invoiced_qty = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_order_line_purchase_order_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_detail",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<short>(type: "smallint", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sku_snapshot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    unit_name_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    tax_rate_snapshot = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    line_subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_tax = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_detail_quote_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_status_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_status_history_quote_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "received_wh_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    withholding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    related_invoice_num = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_received_wh_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_received_wh_detail_received_withholding_withholding_id",
                        column: x => x.withholding_id,
                        principalTable: "received_withholding",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saas_billing_invoice_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_type = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    commercial_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_billing_invoice_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_saas_billing_invoice_lines_saas_billing_invoices_billing_in~",
                        column: x => x.billing_invoice_id,
                        principalTable: "saas_billing_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_note_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, defaultValue: "0"),
                    vat_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_note_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_note_line_sales_note_sales_note_id",
                        column: x => x.sales_note_id,
                        principalTable: "sales_note",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_detail",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sales_order_id = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<short>(type: "smallint", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sku_snapshot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    unit_name_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    tax_rate_snapshot = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    line_subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_tax = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_order_detail_sales_order_sales_order_id",
                        column: x => x.sales_order_id,
                        principalTable: "sales_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_status_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sales_order_id = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_order_status_history_sales_order_sales_order_id",
                        column: x => x.sales_order_id,
                        principalTable: "sales_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "geo_provinces",
                schema: "global",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    country_id = table.Column<string>(type: "character(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geo_provinces", x => x.id);
                    table.ForeignKey(
                        name: "FK_geo_provinces_sri_country_country_id",
                        column: x => x.country_id,
                        principalSchema: "global",
                        principalTable: "sri_country",
                        principalColumn: "iso2",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    access_key = table.Column<string>(type: "character(49)", fixedLength: true, maxLength: 49, nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    doc_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "01"),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    notes_applied = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    payment_terms = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tax_support_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                    validated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoice", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_sri_tax_support_tax_support_code",
                        column: x => x.tax_support_code,
                        principalSchema: "global",
                        principalTable: "sri_tax_support",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_adjustment_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_adjustment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    physical_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    adjustment_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_adjustment_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_adjustment_line_stock_adjustment_stock_adjustment_id",
                        column: x => x.stock_adjustment_id,
                        principalTable: "stock_adjustment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruc = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_provisional_tax_id = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    tax_id_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    main_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country_code = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false, defaultValue: "ECU"),
                    timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "America/Guayaquil"),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    tax_regime_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    is_accounting_req = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    special_taxpayer_no = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_foreign_trade = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    withholds_renta = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    withholds_iva = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    environment_code = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)2),
                    emission_type_code = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    wsdl_recv_test = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    wsdl_auth_test = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    wsdl_recv_prod = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    wsdl_auth_prod = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    logo_base64 = table.Column<string>(type: "text", nullable: true),
                    branding_json = table.Column<string>(type: "jsonb", nullable: true),
                    extra_legend = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    receipt_width_mm = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)80),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    onboarding_completed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    operational_status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_platform_internal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_sri_country_country_code",
                        column: x => x.country_code,
                        principalSchema: "global",
                        principalTable: "sri_country",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_sri_emission_type_emission_type_code",
                        column: x => x.emission_type_code,
                        principalSchema: "global",
                        principalTable: "sri_emission_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_sri_environment_environment_code",
                        column: x => x.environment_code,
                        principalSchema: "global",
                        principalTable: "sri_environment",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_sri_tax_regime_tax_regime_code",
                        column: x => x.tax_regime_code,
                        principalSchema: "global",
                        principalTable: "sri_tax_regime",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_subscribers_subscriber_id",
                        column: x => x.subscriber_id,
                        principalTable: "subscribers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscriber_custom_menus",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    menu_config = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriber_custom_menus", x => x.id);
                    table.ForeignKey(
                        name: "fk_subscriber_custom_menus_subscriber_id",
                        column: x => x.subscriber_id,
                        principalTable: "subscribers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    purchase_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    short_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    observations = table.Column<string>(type: "text", nullable: true),
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subcategory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tariff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applies_vat_on_sale = table.Column<bool>(type: "boolean", nullable: false),
                    applies_vat_on_purchase = table.Column<bool>(type: "boolean", nullable: false),
                    applies_excise_tax = table.Column<bool>(type: "boolean", nullable: false),
                    sale_vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    purchase_vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ice_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    sale_vat_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchase_vat_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    excise_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_service = table.Column<bool>(type: "boolean", nullable: false),
                    tracks_stock = table.Column<bool>(type: "boolean", nullable: false),
                    tracks_lot = table.Column<bool>(type: "boolean", nullable: false),
                    tracks_series = table.Column<bool>(type: "boolean", nullable: false),
                    has_recipe = table.Column<bool>(type: "boolean", nullable: false),
                    stock_with_decimal = table.Column<bool>(type: "boolean", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sale_with_decimal = table.Column<bool>(type: "boolean", nullable: false),
                    max_item_discount_percent = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    available_on_web = table.Column<bool>(type: "boolean", nullable: false),
                    available_on_mobile = table.Column<bool>(type: "boolean", nullable: false),
                    is_ecommerce_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_favorite = table.Column<bool>(type: "boolean", nullable: false),
                    is_for_sale = table.Column<bool>(type: "boolean", nullable: false),
                    base_color = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    has_multiple_colors = table.Column<bool>(type: "boolean", nullable: false),
                    has_sizes = table.Column<bool>(type: "boolean", nullable: false),
                    handles_tariff = table.Column<bool>(type: "boolean", nullable: false),
                    sri_service_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_products_accounts_excise_account_id",
                        column: x => x.excise_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_accounts_purchase_vat_account_id",
                        column: x => x.purchase_vat_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_accounts_sale_vat_account_id",
                        column: x => x.sale_vat_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_brands_brand_id",
                        column: x => x.brand_id,
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_product_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "product_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_product_lines_line_id",
                        column: x => x.line_id,
                        principalTable: "product_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_product_subcategories_subcategory_id",
                        column: x => x.subcategory_id,
                        principalTable: "product_subcategories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_product_types_product_type_id",
                        column: x => x.product_type_id,
                        principalTable: "product_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_sri_ice_rate_ice_code",
                        column: x => x.ice_code,
                        principalSchema: "global",
                        principalTable: "sri_ice_rate",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_sri_uom_uom_code",
                        column: x => x.uom_code,
                        principalSchema: "global",
                        principalTable: "sri_uom",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_sri_vat_rate_purchase_vat_code",
                        column: x => x.purchase_vat_code,
                        principalSchema: "global",
                        principalTable: "sri_vat_rate",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_sri_vat_rate_sale_vat_code",
                        column: x => x.sale_vat_code,
                        principalSchema: "global",
                        principalTable: "sri_vat_rate",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_tariffs_tariff_id",
                        column: x => x.tariff_id,
                        principalTable: "tariffs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ui_nav_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    route_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    label_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    module_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    permission_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    permission_keys_any_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    roles_csv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    saas_feature_definition_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ui_nav_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ui_nav_items_platform_features_saas_feature_definition_id",
                        column: x => x.saas_feature_definition_id,
                        principalTable: "platform_features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ui_nav_items_ui_nav_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "ui_nav_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ui_nav_items_ui_nav_items_parent_item_id",
                        column: x => x.parent_item_id,
                        principalTable: "ui_nav_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_bill",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_discount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    payment_method_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, defaultValue: "01"),
                    payment_days = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_bill", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_bill_master_business_partners_business_partner_id",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_bill_warehouse_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    salesperson_id = table.Column<Guid>(type: "uuid", nullable: true),
                    doc_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    doc_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivery_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reference_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_discount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_notes_applied = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    payment_method_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    payment_days = table.Column<short>(type: "smallint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    remittance_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    guide_doc_num = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_document", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_document_master_business_partners_business_partner_id",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_document_sales_document_reference_document_id",
                        column: x => x.reference_document_id,
                        principalTable: "sales_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_document_warehouse_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequential = table.Column<int>(type: "integer", nullable: false),
                    transfer_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfer", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_transfer_warehouse_source_warehouse_id",
                        column: x => x.source_warehouse_id,
                        principalTable: "warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfer_warehouse_target_warehouse_id",
                        column: x => x.target_warehouse_id,
                        principalTable: "warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bank_statement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    opening_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    closing_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    loaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_reconciled = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_statement", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_statement_bank_account_bank_account_id",
                        column: x => x.bank_account_id,
                        principalTable: "bank_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "petty_cash",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    assigned_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    current_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    replenish_bank_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ledger_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_petty_cash", x => x.id);
                    table.ForeignKey(
                        name: "FK_petty_cash_accounts_ledger_account_id",
                        column: x => x.ledger_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_petty_cash_bank_account_replenish_bank_account_id",
                        column: x => x.replenish_bank_account_id,
                        principalTable: "bank_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "master_bp_carrier_configs",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transport_authorization_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    vehicle_capacity_tons = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_bp_carrier_configs", x => x.role_id);
                    table.ForeignKey(
                        name: "fk_bpcc_role",
                        column: x => x.role_id,
                        principalTable: "master_bp_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "master_bp_supplier_configs",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_tax_support_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    default_retention_vat_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    default_retention_income_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    payment_terms = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_bp_supplier_configs", x => x.role_id);
                    table.ForeignKey(
                        name: "fk_bpsc_role",
                        column: x => x.role_id,
                        principalTable: "master_bp_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purch_retention_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_retention_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    related_invoice = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_retention_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_purch_retention_line_issued_retention_issued_retention_id",
                        column: x => x.issued_retention_id,
                        principalTable: "issued_retention",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purch_note_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_product_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_note_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_purch_note_line_purch_note_purch_note_id",
                        column: x => x.purch_note_id,
                        principalTable: "purch_note",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_withholding_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_withholding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_withholding_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_withholding_line_purchase_withholding_purchase_wit~",
                        column: x => x.purchase_withholding_id,
                        principalTable: "purchase_withholding",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "geo_cantons",
                schema: "global",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    province_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geo_cantons", x => x.id);
                    table.ForeignKey(
                        name: "FK_geo_cantons_geo_provinces_province_id",
                        column: x => x.province_id,
                        principalSchema: "global",
                        principalTable: "geo_provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purch_inv_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 0m),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    ice_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_inv_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_purch_inv_detail_purchase_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "purchase_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_note",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    access_key = table.Column<string>(type: "character(49)", fixedLength: true, maxLength: 49, nullable: true),
                    doc_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    note_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_note", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_note_purchase_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "purchase_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_user_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_user_memberships", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_user_memberships_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "digital_certificate",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    owner_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    issued_at = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_at = table.Column<DateOnly>(type: "date", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_digital_certificate", x => x.id);
                    table.ForeignKey(
                        name: "FK_digital_certificate_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "general_parameter",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_general_parameter", x => x.id);
                    table.ForeignKey(
                        name: "FK_general_parameter_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_barcodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_barcodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_barcodes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_colors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    hex_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_colors", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_colors_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_custom_fields",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    field_type = table.Column<int>(type: "integer", nullable: false),
                    field_value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_custom_fields", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_custom_fields_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_dimensions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    value = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_dimensions", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_dimensions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_features",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    value = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_features", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_features_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    alt_text = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    is_main = table.Column<bool>(type: "boolean", nullable: false),
                    is_ecommerce = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_images_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_sizes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_sizes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_sizes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_substitutes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    substitute_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_substitutes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_substitutes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_supplier_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_supplier_codes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_supplier_codes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_tariff_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_country = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    tariff_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_tariff_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_tariff_details_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_unit_conversions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alternate_uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    conversion_factor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_unit_conversions", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_unit_conversions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_bill_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, defaultValue: "0"),
                    vat_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_bill_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_bill_line_sales_bill_sales_bill_id",
                        column: x => x.sales_bill_id,
                        principalTable: "sales_bill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_retention",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sales_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_retention", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_retention_master_business_partners_business_partner_id",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_retention_sales_bill_sales_bill_id",
                        column: x => x.sales_bill_id,
                        principalTable: "sales_bill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sales_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    vat_percentage = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ice_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_detail_sales_document_sales_document_id",
                        column: x => x.sales_document_id,
                        principalTable: "sales_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_electronic_doc",
                columns: table => new
                {
                    sales_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: true),
                    legacy_electronic_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    doc_type_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    buyer_id_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    buyer_id_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    buyer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    buyer_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    buyer_email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    buyer_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_electronic_doc", x => x.sales_document_id);
                    table.ForeignKey(
                        name: "FK_sales_electronic_doc_sales_document_sales_document_id",
                        column: x => x.sales_document_id,
                        principalTable: "sales_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_payment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    payment_term = table.Column<int>(type: "integer", nullable: true),
                    bank = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_payment", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_payment_sales_document_sales_document_id",
                        column: x => x.sales_document_id,
                        principalTable: "sales_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_withholding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    sales_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issuer_ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    issuer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    voucher_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    total_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_withholding", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_withholding_master_business_partners_business_partner~",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_withholding_sales_document_sales_document_id",
                        column: x => x.sales_document_id,
                        principalTable: "sales_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_transfer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfer_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_transfer_line_stock_transfer_stock_transfer_id",
                        column: x => x.stock_transfer_id,
                        principalTable: "stock_transfer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bank_transaction",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_statement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_transaction", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_transaction_bank_statement_bank_statement_id",
                        column: x => x.bank_statement_id,
                        principalTable: "bank_statement",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bank_transaction_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cash_count",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    petty_cash_id = table.Column<Guid>(type: "uuid", nullable: false),
                    count_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    physical_cash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    difference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_count", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_count_petty_cash_petty_cash_id",
                        column: x => x.petty_cash_id,
                        principalTable: "petty_cash",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "petty_cash_expense",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    petty_cash_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    voucher_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    voucher_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_petty_cash_expense", x => x.id);
                    table.ForeignKey(
                        name: "FK_petty_cash_expense_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_petty_cash_expense_petty_cash_petty_cash_id",
                        column: x => x.petty_cash_id,
                        principalTable: "petty_cash",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "geo_parishes",
                schema: "global",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    canton_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geo_parishes", x => x.id);
                    table.ForeignKey(
                        name: "FK_geo_parishes_geo_cantons_canton_id",
                        column: x => x.canton_id,
                        principalSchema: "global",
                        principalTable: "geo_cantons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_note_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_note_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_note_detail_supplier_note_note_id",
                        column: x => x.note_id,
                        principalTable: "supplier_note",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_retention_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_retention_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_retention_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_retention_line_sales_retention_sales_retention_id",
                        column: x => x.sales_retention_id,
                        principalTable: "sales_retention",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_withholding_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_withholding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_withholding_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_withholding_line_sales_withholding_sales_withholding_~",
                        column: x => x.sales_withholding_id,
                        principalTable: "sales_withholding",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    branch_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phones = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    manager_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    country_id = table.Column<string>(type: "character(10)", maxLength: 10, nullable: true),
                    province_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    canton_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    parish_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    latitude = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    longitude = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    storage_capacity = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    daily_sales_goal = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    recharge_option = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_main_branch = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.id);
                    table.ForeignKey(
                        name: "FK_branches_geo_cantons_canton_id",
                        column: x => x.canton_id,
                        principalSchema: "global",
                        principalTable: "geo_cantons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branches_geo_parishes_parish_id",
                        column: x => x.parish_id,
                        principalSchema: "global",
                        principalTable: "geo_parishes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branches_geo_provinces_province_id",
                        column: x => x.province_id,
                        principalSchema: "global",
                        principalTable: "geo_provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branches_sri_country_country_id",
                        column: x => x.country_id,
                        principalSchema: "global",
                        principalTable: "sri_country",
                        principalColumn: "iso2",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "master_bp_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    location_type = table.Column<short>(type: "smallint", nullable: false),
                    location_purpose = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    address_line = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    province_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    canton_code = table.Column<string>(type: "character(4)", fixedLength: true, maxLength: 4, nullable: true),
                    parish_code = table.Column<string>(type: "character(6)", fixedLength: true, maxLength: 6, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    other_description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_bp_locations", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpl_business_partner",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bpl_canton",
                        column: x => x.canton_code,
                        principalSchema: "global",
                        principalTable: "geo_cantons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bpl_parish",
                        column: x => x.parish_code,
                        principalSchema: "global",
                        principalTable: "geo_parishes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bpl_province",
                        column: x => x.province_code,
                        principalSchema: "global",
                        principalTable: "geo_provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "establishment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_main = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_establishment", x => x.id);
                    table.ForeignKey(
                        name: "FK_establishment_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_establishment_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "master_bp_contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    position = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    contact_role = table.Column<short>(type: "smallint", nullable: false),
                    other_description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_bp_contacts", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpc_business_partner",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bpc_location",
                        column: x => x.location_id,
                        principalTable: "master_bp_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "emission_point",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emission_point", x => x.id);
                    table.ForeignKey(
                        name: "FK_emission_point_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_emission_point_establishment_establishment_id",
                        column: x => x.establishment_id,
                        principalTable: "establishment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_sequence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    current_seq = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_sequence", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_sequence_emission_point_emission_point_id",
                        column: x => x.emission_point_id,
                        principalTable: "emission_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_sequence_sri_doc_type_doc_type_code",
                        column: x => x.doc_type_code,
                        principalSchema: "global",
                        principalTable: "sri_doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "electronic_doc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    establishment_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    emission_point_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    access_key = table.Column<string>(type: "character(49)", fixedLength: true, maxLength: 49, nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    auth_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    subtotal_vat0 = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    subtotal_taxable = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    subtotal_exempt = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    subtotal_no_object = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    subtotal_no_vat_obj = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total_discount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total_vat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total_ice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total_other_taxes = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    additional_info = table.Column<string>(type: "jsonb", nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_electronic_doc", x => x.id);
                    table.ForeignKey(
                        name: "FK_electronic_doc_emission_point_emission_point_id",
                        column: x => x.emission_point_id,
                        principalTable: "emission_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_electronic_doc_sri_doc_type_doc_type_code",
                        column: x => x.doc_type_code,
                        principalSchema: "global",
                        principalTable: "sri_doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_electronic_doc_sri_error_code_error_code",
                        column: x => x.error_code,
                        principalSchema: "global",
                        principalTable: "sri_error_code",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credit_note",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orig_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orig_doc_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "01"),
                    orig_establishment = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    orig_emission_point = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    orig_sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    orig_issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    buyer_id_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    buyer_id_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    buyer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_note", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_note_electronic_doc_id",
                        column: x => x.id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_credit_note_electronic_doc_orig_doc_id",
                        column: x => x.orig_doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "debit_note",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orig_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orig_doc_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "01"),
                    orig_establishment = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    orig_emission_point = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    orig_sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    orig_issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    buyer_id_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    buyer_id_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    buyer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_debit_note", x => x.id);
                    table.ForeignKey(
                        name: "FK_debit_note_electronic_doc_id",
                        column: x => x.id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_debit_note_electronic_doc_orig_doc_id",
                        column: x => x.orig_doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_delivery_detail_electronic_doc_doc_id",
                        column: x => x.doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_guide",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    carrier_ruc = table.Column<string>(type: "character(13)", fixedLength: true, maxLength: 13, nullable: true),
                    carrier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    carrier_plate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    route = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    dest_id_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    dest_id_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    dest_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    dest_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_guide", x => x.id);
                    table.ForeignKey(
                        name: "FK_delivery_guide_electronic_doc_id",
                        column: x => x.id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_delivery_guide_electronic_doc_sales_invoice_id",
                        column: x => x.sales_invoice_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "doc_payment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    payment_term = table.Column<short>(type: "smallint", nullable: true),
                    bank = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_payment", x => x.id);
                    table.ForeignKey(
                        name: "FK_doc_payment_electronic_doc_doc_id",
                        column: x => x.doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_doc_payment_sri_payment_method_payment_method",
                        column: x => x.payment_method,
                        principalSchema: "global",
                        principalTable: "sri_payment_method",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "doc_tax",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<short>(type: "smallint", nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_tax", x => x.id);
                    table.ForeignKey(
                        name: "FK_doc_tax_electronic_doc_doc_id",
                        column: x => x.doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "edoc_invoice_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 0m),
                    discount_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    vat_percentage = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 0m),
                    ice_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ice_percentage = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    ice_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    additional_detail = table.Column<string>(type: "jsonb", nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_edoc_invoice_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_edoc_invoice_detail_electronic_doc_doc_id",
                        column: x => x.doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_edoc_invoice_detail_sri_vat_rate_vat_code",
                        column: x => x.vat_code,
                        principalSchema: "global",
                        principalTable: "sri_vat_rate",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "note_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 0m),
                    vat_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    vat_percentage = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 0m),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    sort_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_note_detail_electronic_doc_doc_id",
                        column: x => x.doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_settlement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_id_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    seller_id_num = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    seller_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    seller_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_settlement", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_settlement_electronic_doc_id",
                        column: x => x.id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buyer_id_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    buyer_id_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    buyer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    buyer_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    buyer_email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    buyer_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    salesperson_id = table.Column<Guid>(type: "uuid", nullable: true),
                    delivery_date = table.Column<DateOnly>(type: "date", nullable: true),
                    remittance_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    guide_doc_num = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_invoice_electronic_doc_id",
                        column: x => x.id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sales_invoice_sri_id_type_buyer_id_type",
                        column: x => x.buyer_id_type,
                        principalSchema: "global",
                        principalTable: "sri_id_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "withholding_cert",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_ruc = table.Column<string>(type: "character(13)", fixedLength: true, maxLength: 13, nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    purchase_inv_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withholding_cert", x => x.id);
                    table.ForeignKey(
                        name: "FK_withholding_cert_electronic_doc_id",
                        column: x => x.id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "withholding_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    retained_doc_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    retained_doc_num = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    retained_doc_date = table.Column<DateOnly>(type: "date", nullable: true),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_support_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    WithholdingCertificateId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withholding_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_withholding_detail_electronic_doc_doc_id",
                        column: x => x.doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_withholding_detail_sri_tax_support_tax_support_code",
                        column: x => x.tax_support_code,
                        principalSchema: "global",
                        principalTable: "sri_tax_support",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withholding_detail_withholding_cert_WithholdingCertificateId",
                        column: x => x.WithholdingCertificateId,
                        principalTable: "withholding_cert",
                        principalColumn: "id");
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_country",
                columns: new[] { "code", "is_active", "iso2", "name", "phone_code" },
                values: new object[,]
                {
                    { "ARG", true, "AR", "ARGENTINA", "+54" },
                    { "AUS", true, "AU", "AUSTRALIA", "+61" },
                    { "BOL", true, "BO", "BOLIVIA", "+591" },
                    { "BRA", true, "BR", "BRASIL", "+55" },
                    { "CAN", true, "CA", "CANADÃ", "+1" },
                    { "CHL", true, "CL", "CHILE", "+56" },
                    { "CHN", true, "CN", "CHINA", "+86" },
                    { "COL", true, "CO", "COLOMBIA", "+57" },
                    { "CRI", true, "CR", "COSTA RICA", "+506" },
                    { "DEU", true, "DE", "ALEMANIA", "+49" },
                    { "DOM", true, "DO", "REPÃšBLICA DOMINICANA", "+1" },
                    { "ECU", true, "EC", "ECUADOR", "+593" },
                    { "ESP", true, "ES", "ESPAÃ‘A", "+34" },
                    { "FRA", true, "FR", "FRANCIA", "+33" },
                    { "GBR", true, "GB", "REINO UNIDO", "+44" },
                    { "GTM", true, "GT", "GUATEMALA", "+502" },
                    { "HND", true, "HN", "HONDURAS", "+504" },
                    { "IND", true, "IN", "INDIA", "+91" },
                    { "ITA", true, "IT", "ITALIA", "+39" },
                    { "JPN", true, "JP", "JAPÃ“N", "+81" },
                    { "MEX", true, "MX", "MÃ‰XICO", "+52" },
                    { "NIC", true, "NI", "NICARAGUA", "+505" },
                    { "PAN", true, "PA", "PANAMÃ", "+507" },
                    { "PER", true, "PE", "PERÃš", "+51" },
                    { "PRY", true, "PY", "PARAGUAY", "+595" },
                    { "SLV", true, "SV", "EL SALVADOR", "+503" },
                    { "URY", true, "UY", "URUGUAY", "+598" },
                    { "USA", true, "US", "ESTADOS UNIDOS", "+1" },
                    { "VEN", true, "VE", "VENEZUELA", "+58" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_doc_type",
                columns: new[] { "code", "is_active", "is_electronic", "name", "short_name" },
                values: new object[,]
                {
                    { "01", true, true, "salesBill", "salesBill" },
                    { "02", true, true, "note de Venta - RISE", "NV_RISE" },
                    { "03", true, true, "LiquidaciÃ³n de Compra de Bienes y PrestaciÃ³n de Servicios", "LIQ_COMPRA" },
                    { "04", true, true, "note de CrÃ©dito", "N_CREDITO" },
                    { "05", true, true, "note de DÃ©bito", "N_DEBITO" },
                    { "06", true, true, "GuÃ­a de RemisiÃ³n", "G_REMISION" },
                    { "07", true, true, "Comprobante de RetenciÃ³n", "retention" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_doc_type",
                columns: new[] { "code", "name", "short_name" },
                values: new object[,]
                {
                    { "08", "Tiquete de MÃ¡quina Registradora", "TIQUETE" },
                    { "09", "Tiquete de Caja Registradora", "CAJA_REG" },
                    { "18", "Documento ElectrÃ³nico de ImportaciÃ³n", "DEI" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_emission_type",
                columns: new[] { "code", "name" },
                values: new object[,]
                {
                    { (short)1, "EmisiÃ³n Normal" },
                    { (short)2, "EmisiÃ³n por Indisponibilidad del Sistema" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_environment",
                columns: new[] { "code", "abbrev", "name" },
                values: new object[,]
                {
                    { (short)1, "PROD", "ProducciÃ³n" },
                    { (short)2, "TEST", "Pruebas" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_error_code",
                columns: new[] { "code", "description", "error_type", "is_active", "name" },
                values: new object[,]
                {
                    { "102", null, "ERROR", true, "CLAVE DE ACCESO NO EXISTE" },
                    { "300", null, "WARNING", true, "COMPROBANTE NO AUTORIZADO" },
                    { "301", null, "ERROR", true, "CLAVE DE ACCESO INCORRECTA" },
                    { "35", null, "ERROR", true, "CLAVE DE ACCESO REGISTRADA" },
                    { "43", null, "ERROR", true, "XML NO CUMPLE ESPECIFICACIONES" },
                    { "60", null, "WARNING", true, "FIRMA INVÃLIDA" },
                    { "65", null, "ERROR", true, "AMBIENTE NO VÃLIDO" },
                    { "70", null, "ERROR", true, "CLAVE DE ACCESO NO REGISTRADA" },
                    { "72", null, "ERROR", true, "NÃšMERO DE COMPROBANTE YA EXISTE" },
                    { "73", null, "ERROR", true, "CLAVE DE ACCESO INVÃLIDA" },
                    { "90", null, "ERROR", true, "CERTIFICADO INVÃLIDO O CADUCADO" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_ice_rate",
                columns: new[] { "code", "is_active", "name", "percentage", "unit_value" },
                values: new object[,]
                {
                    { "3011", true, "Cigarrillos rubios importados", 150.00m, null },
                    { "3021", true, "Cigarrillos negros nacionales", 150.00m, null },
                    { "3041", true, "Bebidas gaseosas con azÃºcar aÃ±adida", 10.00m, null },
                    { "3051", true, "Bebidas energizantes", 10.00m, null },
                    { "3071", true, "Perfumes y aguas de tocador", 20.00m, null },
                    { "3072", true, "Videojuegos", 35.00m, null },
                    { "3073", true, "Armas de fuego deportivas", 300.00m, null },
                    { "3081", true, "VehÃ­culos â‰¤3.5t (hasta USD 30k)", 5.00m, null },
                    { "3082", true, "VehÃ­culos â‰¤3.5t (USD 30kâ€“40k)", 10.00m, null },
                    { "3083", true, "VehÃ­culos â‰¤3.5t (mÃ¡s de USD 40k)", 15.00m, null },
                    { "3091", true, "Aviones / helicÃ³pteros de uso privado", 15.00m, null },
                    { "3101", true, "Servicios de televisiÃ³n pagada", 15.00m, null },
                    { "3111", true, "Bebidas alcohÃ³licas (incl. cerveza)", 75.00m, null }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_id_type",
                columns: new[] { "code", "digits", "name" },
                values: new object[,]
                {
                    { "04", (short)13, "Registro Ãšnico de Contribuyentes" },
                    { "05", (short)10, "CÃ©dula de ciudadanÃ­a" },
                    { "06", null, "Pasaporte" },
                    { "07", null, "Consumidor Final" },
                    { "08", null, "IdentificaciÃ³n del exterior" },
                    { "09", null, "Placa" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_payment_method",
                columns: new[] { "code", "is_active", "name" },
                values: new object[,]
                {
                    { "01", true, "Sin utilizaciÃ³n del sistema financiero" },
                    { "15", true, "CompensaciÃ³n de deudas" },
                    { "16", true, "Tarjeta de dÃ©bito" },
                    { "17", true, "Dinero electrÃ³nico" },
                    { "18", true, "Tarjeta prepago" },
                    { "19", true, "Tarjeta de crÃ©dito" },
                    { "20", true, "Otros con utilizaciÃ³n del sistema financiero" },
                    { "21", true, "Endoso de tÃ­tulos" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_retention_code",
                columns: new[] { "id", "applies_to", "code", "is_active", "name", "percentage", "tax_type" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "SUPPLIER", "721", true, "Ret. IVA 10% â€“ Bienes (tarifa vigente)", 10.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "SUPPLIER", "723", true, "Ret. IVA 20% â€“ Servicios (tarifa vigente)", 20.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "SUPPLIER", "725", true, "Ret. IVA 30% â€“ Presuntivo bienes", 30.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "SUPPLIER", "726", true, "Ret. IVA 70% â€“ Presuntivo servicios", 70.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "SUPPLIER", "727", true, "Ret. IVA 100% â€“ Liq. compra / honorarios", 100.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "SUPPLIER", "728", true, "Ret. IVA 15% â€“ Constructoras", 15.00m, "IVA" },
                    { new Guid("20000000-0000-0000-0000-000000000001"), "SUPPLIER", "303", true, "Honorarios profesionales y demÃ¡s servicios", 10.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "SUPPLIER", "304", true, "Servicios â€“ predomina mano de obra", 2.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "SUPPLIER", "307", true, "Publicidad y comunicaciÃ³n", 1.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "SUPPLIER", "309", true, "Arrendamiento bienes inmuebles (persona natural)", 8.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000005"), "SUPPLIER", "310", true, "Seguros y reaseguros (10% de primas)", 1.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000006"), "SUPPLIER", "312", true, "Transf. bienes muebles de naturaleza corporal", 1.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000007"), "SUPPLIER", "320", true, "Servicios entre sociedades", 2.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000008"), "SUPPLIER", "325", true, "Compra bienes corporales muebles", 1.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000009"), "SUPPLIER", "327", true, "Actividades de construcciÃ³n (contrato)", 1.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000010"), "SUPPLIER", "341", true, "Otras retenciones aplicables al 2%", 2.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000011"), "SUPPLIER", "342", true, "Otras retenciones aplicables al 1%", 1.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000012"), "SUPPLIER", "343", true, "Otras retenciones aplicables al 1.75%", 1.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000013"), "SUPPLIER", "344", true, "Otras retenciones aplicables al 2.75%", 2.75m, "RENTA" },
                    { new Guid("30000000-0000-0000-0000-000000000001"), "SUPPLIER", "4580", true, "ISD â€“ Impuesto a la Salida de Divisas", 5.00m, "ISD" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_tax_regime",
                columns: new[] { "code", "abbrev", "is_active", "name" },
                values: new object[,]
                {
                    { "01", "GENERAL", true, "RÃ©gimen General" },
                    { "02", "RIMPE_ME", true, "RIMPE â€“ RÃ©gimen de Microempresas" },
                    { "03", "RIMPE_NP", true, "RIMPE â€“ Negocio Popular" },
                    { "04", "ESP", true, "Contribuyente Especial" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_tax_support",
                columns: new[] { "code", "is_active", "name" },
                values: new object[,]
                {
                    { "01", true, "CrÃ©dito Tributario para declaraciÃ³n de IVA" },
                    { "02", true, "Costo o Gasto para declaraciÃ³n del IR" },
                    { "03", true, "Activo Fijo â€“ CrÃ©dito Tributario IVA" },
                    { "04", true, "Activo Fijo â€“ Costo o Gasto IR" },
                    { "05", true, "LiquidaciÃ³n Gastos de Viaje, Hospedaje y AlimentaciÃ³n" },
                    { "06", true, "RetenciÃ³n en la Fuente" },
                    { "07", true, "DistribuciÃ³n de Dividendos, Beneficios o Ganancias" },
                    { "08", true, "Impuesto a los Activos en el Exterior" },
                    { "09", true, "RetenciÃ³n del IVA 30%" },
                    { "10", true, "RetenciÃ³n del IVA 70%" },
                    { "11", true, "RetenciÃ³n del IVA 100%" },
                    { "12", true, "ExportaciÃ³n de Bienes" },
                    { "13", true, "No aplica" },
                    { "14", true, "ExportaciÃ³n de servicios con domicilio en el exterior" },
                    { "15", true, "Proveedor directo de exportador de bienes" },
                    { "16", true, "Provisiones de cuentas incobrables" },
                    { "17", true, "note de crÃ©dito deducible" },
                    { "18", true, "Importaciones" },
                    { "19", true, "Reembolso de gastos" },
                    { "20", true, "Notas de crÃ©dito por devoluciones" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_uom",
                columns: new[] { "code", "abbrev", "is_active", "name" },
                values: new object[,]
                {
                    { "01", "UB", true, "Unidad BiolÃ³gica" },
                    { "02", "CAJA", true, "Caja" },
                    { "03", "DEC", true, "Decena" },
                    { "04", "DOC", true, "Docena (12 un.)" },
                    { "05", "FARDO", true, "Fardo" },
                    { "06", "G", true, "Gramo" },
                    { "07", "KG", true, "Kilogramo" },
                    { "08", "LB", true, "Libra" },
                    { "09", "LT", true, "Litro" },
                    { "10", "M", true, "Metro" },
                    { "11", "M2", true, "Metro cuadrado" },
                    { "12", "M3", true, "Metro cÃºbico" },
                    { "13", "ML", true, "Mililitro" },
                    { "14", "PAQ", true, "Paquete" },
                    { "15", "PAR", true, "Par" },
                    { "16", "QQ", true, "Quintal" },
                    { "17", "ROLLO", true, "Rollo" },
                    { "18", "TON", true, "Tonelada" },
                    { "19", "UN", true, "Unidad" },
                    { "20", "VEH", true, "VehÃ­culo" },
                    { "21", "SET", true, "Set" },
                    { "22", "SURT", true, "Surtido" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_vat_rate",
                columns: new[] { "code", "is_active", "name", "percentage", "valid_from", "valid_until" },
                values: new object[,]
                {
                    { "0", true, "0% IVA", 0.00m, new DateOnly(2008, 1, 1), null },
                    { "10", true, "15% IVA (vigente)", 15.00m, new DateOnly(2024, 4, 1), null }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_vat_rate",
                columns: new[] { "code", "name", "percentage", "valid_from", "valid_until" },
                values: new object[,]
                {
                    { "11", "13% IVA (transitorio)", 13.00m, new DateOnly(2023, 1, 1), new DateOnly(2023, 12, 31) },
                    { "2", "12% IVA (histÃ³rico)", 12.00m, new DateOnly(2008, 1, 1), new DateOnly(2016, 5, 31) },
                    { "3", "14% IVA (histÃ³rico)", 14.00m, new DateOnly(2016, 6, 1), new DateOnly(2017, 5, 31) }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_vat_rate",
                columns: new[] { "code", "is_active", "name", "percentage", "valid_from", "valid_until" },
                values: new object[,]
                {
                    { "4", true, "No Objeto de IVA", 0.00m, new DateOnly(2008, 1, 1), null },
                    { "5", true, "Exento de IVA", 0.00m, new DateOnly(2008, 1, 1), null },
                    { "6", true, "No Objeto IVA (Serv.)", 0.00m, new DateOnly(2008, 1, 1), null },
                    { "7", true, "Diferencial de precio", 0.00m, new DateOnly(2008, 1, 1), null },
                    { "8", true, "5% IVA (reducido)", 5.00m, new DateOnly(2024, 1, 1), null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_profile_permissions_subscriber_key",
                table: "access_profile_permissions",
                columns: new[] { "subscriber_id", "permission_key" });

            migrationBuilder.CreateIndex(
                name: "ux_access_profile_permissions_subscriber_profile_key",
                table: "access_profile_permissions",
                columns: new[] { "subscriber_id", "profile_id", "permission_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_access_profiles_subscriber_name",
                table: "access_profiles",
                columns: new[] { "subscriber_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounting_periods_subscriber_closed",
                table: "accounting_periods",
                columns: new[] { "subscriber_id", "is_closed" });

            migrationBuilder.CreateIndex(
                name: "ix_accounting_periods_subscriber_company",
                table: "accounting_periods",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_accounting_periods_subscriber_company_year_month",
                table: "accounting_periods",
                columns: new[] { "subscriber_id", "company_id", "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_bank_account_id",
                table: "accounting_setup",
                column: "bank_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_cash_account_id",
                table: "accounting_setup",
                column: "cash_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_cost_of_sales_account_id",
                table: "accounting_setup",
                column: "cost_of_sales_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_customers_account_id",
                table: "accounting_setup",
                column: "customers_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_inventory_account_id",
                table: "accounting_setup",
                column: "inventory_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_sales_account_id",
                table: "accounting_setup",
                column: "sales_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_suppliers_account_id",
                table: "accounting_setup",
                column: "suppliers_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_vat_purchases_account_id",
                table: "accounting_setup",
                column: "vat_purchases_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_vat_sales_account_id",
                table: "accounting_setup",
                column: "vat_sales_account_id");

            migrationBuilder.CreateIndex(
                name: "uq_accounting_setup_subscriber",
                table: "accounting_setup",
                column: "subscriber_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_accounting_setup_subscriber_company",
                table: "accounting_setup",
                columns: new[] { "subscriber_id", "company_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_subscriber_code",
                table: "accounts",
                columns: new[] { "subscriber_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_subscriber_company",
                table: "accounts",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ap_entries_purch_bill",
                table: "ap_entries",
                column: "purch_bill_id",
                filter: "purch_bill_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ap_entries_subscriber_company_status",
                table: "ap_entries",
                columns: new[] { "subscriber_id", "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_ap_entries_subscriber_due_status",
                table: "ap_entries",
                columns: new[] { "subscriber_id", "due_date", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_app_features_parent_id",
                table: "app_features",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "uq_app_features_permission",
                table: "app_features",
                column: "permission",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ar_entries_sales_bill",
                table: "ar_entries",
                column: "sales_bill_id",
                filter: "sales_bill_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ar_entries_subscriber_company_status",
                table: "ar_entries",
                columns: new[] { "subscriber_id", "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_ar_entries_subscriber_due_status",
                table: "ar_entries",
                columns: new[] { "subscriber_id", "due_date", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_account_ledger_account_id",
                table: "bank_account",
                column: "ledger_account_id");

            migrationBuilder.CreateIndex(
                name: "uq_bank_account_subscriber_number",
                table: "bank_account",
                columns: new[] { "subscriber_id", "account_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_statement_bank_account_id",
                table: "bank_statement",
                column: "bank_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_statement_subscriber_account_period",
                table: "bank_statement",
                columns: new[] { "subscriber_id", "bank_account_id", "period_from", "period_to" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_bank_statement_id",
                table: "bank_transaction",
                column: "bank_statement_id");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_journal_entry_id",
                table: "bank_transaction",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_transaction_subscriber_statement_date",
                table: "bank_transaction",
                columns: new[] { "subscriber_id", "bank_statement_id", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "ix_billing_checkout_sessions_subscriber",
                table: "billing_checkout_sessions",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "ix_billing_payment_attempts_subscriber_invoice",
                table: "billing_payment_attempts",
                columns: new[] { "subscriber_id", "invoice_id", "attempt_number" });

            migrationBuilder.CreateIndex(
                name: "IX_branches_canton_id",
                table: "branches",
                column: "canton_id");

            migrationBuilder.CreateIndex(
                name: "IX_branches_country_id",
                table: "branches",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "IX_branches_parish_id",
                table: "branches",
                column: "parish_id");

            migrationBuilder.CreateIndex(
                name: "IX_branches_province_id",
                table: "branches",
                column: "province_id");

            migrationBuilder.CreateIndex(
                name: "ix_branches_subscriber_id",
                table: "branches",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "ix_brands_subscriber_code",
                table: "brands",
                columns: new[] { "subscriber_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_carriers_subscriber_id",
                table: "carriers",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "ux_carriers_subscriber_identification",
                table: "carriers",
                columns: new[] { "subscriber_id", "identification_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_count_petty_cash_id",
                table: "cash_count",
                column: "petty_cash_id");

            migrationBuilder.CreateIndex(
                name: "ux_commercial_plan_features_plan_feature",
                table: "commercial_plan_features",
                columns: new[] { "plan_id", "feature_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_commercial_plan_limits_plan_code",
                table: "commercial_plan_limits",
                columns: new[] { "commercial_plan_id", "limit_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_commercial_plans_code",
                table: "commercial_plans",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_country_code",
                table: "company",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "IX_company_emission_type_code",
                table: "company",
                column: "emission_type_code");

            migrationBuilder.CreateIndex(
                name: "IX_company_environment_code",
                table: "company",
                column: "environment_code");

            migrationBuilder.CreateIndex(
                name: "ix_company_platform_internal",
                table: "company",
                column: "is_platform_internal",
                filter: "is_platform_internal = true");

            migrationBuilder.CreateIndex(
                name: "ix_company_subscriber_id",
                table: "company",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_tax_regime_code",
                table: "company",
                column: "tax_regime_code");

            migrationBuilder.CreateIndex(
                name: "uq_company_ruc",
                table: "company",
                column: "ruc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_company_user_memberships_company_identity_user",
                table: "company_user_memberships",
                columns: new[] { "company_id", "identity_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_config_feature_subscriber_feature_key",
                table: "config_feature",
                columns: new[] { "subscriber_id", "feature", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_config_global_subscriber_key",
                table: "config_global",
                columns: new[] { "subscriber_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_config_module_subscriber_module_key",
                table: "config_module",
                columns: new[] { "subscriber_id", "module", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_cn_orig",
                table: "credit_note",
                column: "orig_doc_id");

            migrationBuilder.CreateIndex(
                name: "ix_current_stock_subscriber_company",
                table: "current_stock",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_current_stock_subscriber_product_warehouse",
                table: "current_stock",
                columns: new[] { "subscriber_id", "product_id", "warehouse_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_debit_note_orig_doc_id",
                table: "debit_note",
                column: "orig_doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_detail_doc_id",
                table: "delivery_detail",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_guide_sales_invoice_id",
                table: "delivery_guide",
                column: "sales_invoice_id");

            migrationBuilder.CreateIndex(
                name: "idx_cert_company",
                table: "digital_certificate",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_digital_certificate_subscriber_id",
                table: "digital_certificate",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "idx_doc_payment",
                table: "doc_payment",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_doc_payment_payment_method",
                table: "doc_payment",
                column: "payment_method");

            migrationBuilder.CreateIndex(
                name: "idx_doc_tax",
                table: "doc_tax",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_relation_target",
                table: "document_relation",
                columns: new[] { "subscriber_id", "target_module", "target_id" });

            migrationBuilder.CreateIndex(
                name: "uq_document_relation_source",
                table: "document_relation",
                columns: new[] { "subscriber_id", "source_module", "source_id", "relation_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_docseq_company",
                table: "document_sequence",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_docseq_subscriber_id",
                table: "document_sequence",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_sequence_doc_type_code",
                table: "document_sequence",
                column: "doc_type_code");

            migrationBuilder.CreateIndex(
                name: "uq_doc_seq",
                table: "document_sequence",
                columns: new[] { "emission_point_id", "doc_type_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_inv_det_doc",
                table: "edoc_invoice_detail",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "idx_inv_det_prod",
                table: "edoc_invoice_detail",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_edoc_invoice_detail_vat_code",
                table: "edoc_invoice_detail",
                column: "vat_code");

            migrationBuilder.CreateIndex(
                name: "idx_edoc_access_key",
                table: "electronic_doc",
                column: "access_key",
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_edoc_company",
                table: "electronic_doc",
                columns: new[] { "company_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "idx_edoc_status",
                table: "electronic_doc",
                columns: new[] { "company_id", "status", "doc_type_code" });

            migrationBuilder.CreateIndex(
                name: "IX_electronic_doc_doc_type_code",
                table: "electronic_doc",
                column: "doc_type_code");

            migrationBuilder.CreateIndex(
                name: "IX_electronic_doc_emission_point_id",
                table: "electronic_doc",
                column: "emission_point_id");

            migrationBuilder.CreateIndex(
                name: "IX_electronic_doc_error_code",
                table: "electronic_doc",
                column: "error_code");

            migrationBuilder.CreateIndex(
                name: "ix_electronic_doc_subscriber_id",
                table: "electronic_doc",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "uq_edoc_seq",
                table: "electronic_doc",
                columns: new[] { "company_id", "doc_type_code", "establishment_code", "emission_point_code", "sequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_emission_point_company_id",
                table: "emission_point",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_emission_point_subscriber_id",
                table: "emission_point",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "uq_ep_code",
                table: "emission_point",
                columns: new[] { "establishment_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_establishment_branch_id",
                table: "establishment",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_establishment_subscriber_id",
                table: "establishment",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "uq_estab_code",
                table: "establishment",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expense_category_expense_account_id",
                table: "expense_category",
                column: "expense_account_id");

            migrationBuilder.CreateIndex(
                name: "uq_expense_category_subscriber_cat",
                table: "expense_category",
                columns: new[] { "subscriber_id", "category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_detail_expense_id",
                table: "expense_detail",
                column: "expense_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_detail_expense_sort",
                table: "expense_detail",
                columns: new[] { "expense_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_invoice_subscriber_category",
                table: "expense_invoice",
                columns: new[] { "subscriber_id", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_invoice_subscriber_date",
                table: "expense_invoice",
                columns: new[] { "subscriber_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_invoice_subscriber_status",
                table: "expense_invoice",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_expense_invoice_access_key",
                table: "expense_invoice",
                columns: new[] { "subscriber_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_general_parameter_subscriber_id",
                table: "general_parameter",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "uq_gen_param",
                table: "general_parameter",
                columns: new[] { "company_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_geo_cantons_province_id",
                schema: "global",
                table: "geo_cantons",
                column: "province_id");

            migrationBuilder.CreateIndex(
                name: "ix_geo_parishes_canton_id",
                schema: "global",
                table: "geo_parishes",
                column: "canton_id");

            migrationBuilder.CreateIndex(
                name: "ix_geo_provinces_country_id",
                schema: "global",
                table: "geo_provinces",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "ux_identity_users_email",
                table: "identity_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_identity_users_email_normalized",
                table: "identity_users",
                column: "email_normalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoice_subscriber_business_partner",
                table: "invoice",
                columns: new[] { "subscriber_id", "business_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_subscriber_issue_date",
                table: "invoice",
                columns: new[] { "subscriber_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_subscriber_status",
                table: "invoice",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_invoice_public_id",
                table: "invoice",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_invoice_seq",
                table: "invoice",
                columns: new[] { "subscriber_id", "estab_code", "em_point_code", "sequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_detail_invoice_id",
                table: "invoice_detail",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "uq_invoice_detail_line",
                table: "invoice_detail",
                columns: new[] { "subscriber_id", "invoice_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_electronic_invoice_id",
                table: "invoice_electronic",
                column: "invoice_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_invoice_electronic_invoice",
                table: "invoice_electronic",
                columns: new[] { "subscriber_id", "invoice_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoice_status_history_invoice",
                table: "invoice_status_history",
                columns: new[] { "subscriber_id", "invoice_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_status_history_invoice_id",
                table: "invoice_status_history",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_issued_retention_business_partner_id",
                table: "issued_retention",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_issued_retention_purch_bill_id",
                table: "issued_retention",
                column: "purch_bill_id");

            migrationBuilder.CreateIndex(
                name: "uq_issued_retention_seq",
                table: "issued_retention",
                columns: new[] { "subscriber_id", "establishment_code", "emission_point_code", "sequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_subscriber_company",
                table: "journal_entries",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_subscriber_reference",
                table: "journal_entries",
                columns: new[] { "subscriber_id", "reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_journal_entry_id",
                table: "journal_entry_lines",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_kardex_report_requested_at",
                table: "kardex_report",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "ix_kardex_report_subscriber_status",
                table: "kardex_report",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_kardex_snapshot_subscriber_date",
                table: "kardex_snapshot",
                columns: new[] { "subscriber_id", "snapshot_date" });

            migrationBuilder.CreateIndex(
                name: "uq_kardex_snapshot_lookup",
                table: "kardex_snapshot",
                columns: new[] { "subscriber_id", "product_id", "warehouse_id", "snapshot_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bpc_location",
                table: "master_bp_contacts",
                column: "location_id",
                filter: "location_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_bpc_subscriber_bp_active",
                table: "master_bp_contacts",
                columns: new[] { "subscriber_id", "business_partner_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_bpc_subscriber_bp_role",
                table: "master_bp_contacts",
                columns: new[] { "subscriber_id", "business_partner_id", "contact_role" });

            migrationBuilder.CreateIndex(
                name: "IX_master_bp_contacts_business_partner_id",
                table: "master_bp_contacts",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "uq_bpc_primary",
                table: "master_bp_contacts",
                columns: new[] { "subscriber_id", "business_partner_id" },
                unique: true,
                filter: "is_primary = true AND is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_bpl_subscriber_bp_active",
                table: "master_bp_locations",
                columns: new[] { "subscriber_id", "business_partner_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_master_bp_locations_business_partner_id",
                table: "master_bp_locations",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_master_bp_locations_canton_code",
                table: "master_bp_locations",
                column: "canton_code");

            migrationBuilder.CreateIndex(
                name: "IX_master_bp_locations_parish_code",
                table: "master_bp_locations",
                column: "parish_code");

            migrationBuilder.CreateIndex(
                name: "IX_master_bp_locations_province_code",
                table: "master_bp_locations",
                column: "province_code");

            migrationBuilder.CreateIndex(
                name: "uq_bpl_primary",
                table: "master_bp_locations",
                columns: new[] { "subscriber_id", "business_partner_id" },
                unique: true,
                filter: "is_primary = true AND is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_bpr_subscriber_bp",
                table: "master_bp_roles",
                columns: new[] { "subscriber_id", "business_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bpr_subscriber_type",
                table: "master_bp_roles",
                columns: new[] { "subscriber_id", "role_type" });

            migrationBuilder.CreateIndex(
                name: "ix_bpr_subscriber_type_active",
                table: "master_bp_roles",
                columns: new[] { "subscriber_id", "role_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_master_bp_roles_business_partner_id",
                table: "master_bp_roles",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "uq_bpr_bp_role",
                table: "master_bp_roles",
                columns: new[] { "subscriber_id", "business_partner_id", "role_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mbp_subscriber",
                table: "master_business_partners",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "ix_mbp_subscriber_active",
                table: "master_business_partners",
                columns: new[] { "subscriber_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_cbts_blocked",
                table: "master_company_bp_trading_settings",
                columns: new[] { "subscriber_id", "company_id" },
                filter: "is_blocked = true");

            migrationBuilder.CreateIndex(
                name: "IX_master_company_bp_trading_settings_business_partner_id",
                table: "master_company_bp_trading_settings",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "uq_cbts_company_bp",
                table: "master_company_bp_trading_settings",
                columns: new[] { "subscriber_id", "company_id", "business_partner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_note_det_doc",
                table: "note_detail",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_EventName",
                table: "OutboxMessages",
                columns: new[] { "EventName", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Subscriber",
                table: "OutboxMessages",
                columns: new[] { "SubscriberId", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_user",
                table: "password_reset_tokens",
                columns: new[] { "user_id", "user_kind", "subscriber_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_applications_ap_entry",
                table: "payment_applications",
                column: "ap_entry_id",
                filter: "ap_entry_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payment_applications_ar_entry",
                table: "payment_applications",
                column: "ar_entry_id",
                filter: "ar_entry_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payment_applications_subscriber_date",
                table: "payment_applications",
                columns: new[] { "subscriber_id", "application_date" });

            migrationBuilder.CreateIndex(
                name: "ux_payment_provider_customers_sub_provider",
                table: "payment_provider_customers",
                columns: new[] { "subscriber_id", "provider_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_payment_provider_subscriptions_sub_provider",
                table: "payment_provider_subscriptions",
                columns: new[] { "subscriber_id", "provider_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_ledger_account_id",
                table: "petty_cash",
                column: "ledger_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_replenish_bank_account_id",
                table: "petty_cash",
                column: "replenish_bank_account_id");

            migrationBuilder.CreateIndex(
                name: "uq_petty_cash_subscriber_name",
                table: "petty_cash",
                columns: new[] { "subscriber_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_expense_journal_entry_id",
                table: "petty_cash_expense",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_expense_petty_cash_id",
                table: "petty_cash_expense",
                column: "petty_cash_id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_action",
                table: "platform_audit_logs",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_actor_user",
                table: "platform_audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_created_at",
                table: "platform_audit_logs",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_target_subscriber",
                table: "platform_audit_logs",
                column: "target_subscriber_id");

            migrationBuilder.CreateIndex(
                name: "ux_platform_features_code",
                table: "platform_features",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_platform_provisioning_audit_event_type",
                table: "platform_provisioning_audit",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_platform_provisioning_audit_timestamp",
                table: "platform_provisioning_audit",
                column: "timestamp_utc");

            migrationBuilder.CreateIndex(
                name: "ix_processed_webhook_events_processed_at",
                table: "processed_webhook_events",
                column: "processed_at_utc");

            migrationBuilder.CreateIndex(
                name: "ux_processed_webhook_events_provider_event_id",
                table: "processed_webhook_events",
                column: "provider_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_barcodes_product_id",
                table: "product_barcodes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_barcodes_subscriber_code",
                table: "product_barcodes",
                columns: new[] { "subscriber_id", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_product_categories_subscriber_line",
                table: "product_categories",
                columns: new[] { "subscriber_id", "line_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_categories_subscriber_line_code",
                table: "product_categories",
                columns: new[] { "subscriber_id", "line_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_colors_product_id",
                table: "product_colors",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_colors_subscriber_name",
                table: "product_colors",
                columns: new[] { "subscriber_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_custom_fields_product_id",
                table: "product_custom_fields",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_custom_fields_subscriber_field_name",
                table: "product_custom_fields",
                columns: new[] { "subscriber_id", "field_name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_dimensions_product_id",
                table: "product_dimensions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_dimensions_subscriber_name",
                table: "product_dimensions",
                columns: new[] { "subscriber_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_features_product_id",
                table: "product_features",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_features_subscriber_name",
                table: "product_features",
                columns: new[] { "subscriber_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_images_product_id",
                table: "product_images",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_images_subscriber_is_ecommerce",
                table: "product_images",
                columns: new[] { "subscriber_id", "is_ecommerce" });

            migrationBuilder.CreateIndex(
                name: "ix_product_images_subscriber_is_main",
                table: "product_images",
                columns: new[] { "subscriber_id", "is_main" });

            migrationBuilder.CreateIndex(
                name: "ix_product_lines_subscriber_code",
                table: "product_lines",
                columns: new[] { "subscriber_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_sizes_product_id",
                table: "product_sizes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_sizes_subscriber_name",
                table: "product_sizes",
                columns: new[] { "subscriber_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_subcategories_subscriber_category",
                table: "product_subcategories",
                columns: new[] { "subscriber_id", "category_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_subcategories_subscriber_category_code",
                table: "product_subcategories",
                columns: new[] { "subscriber_id", "category_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_substitutes_product_id",
                table: "product_substitutes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_substitutes_subscriber_substitute",
                table: "product_substitutes",
                columns: new[] { "subscriber_id", "substitute_product_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_supplier_codes_product_id",
                table: "product_supplier_codes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_supplier_codes_subscriber_code",
                table: "product_supplier_codes",
                columns: new[] { "subscriber_id", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_product_supplier_codes_subscriber_supplier",
                table: "product_supplier_codes",
                columns: new[] { "subscriber_id", "business_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_tariff_details_product_id",
                table: "product_tariff_details",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_tariff_details_subscriber_country",
                table: "product_tariff_details",
                columns: new[] { "subscriber_id", "origin_country" });

            migrationBuilder.CreateIndex(
                name: "ix_product_types_subscriber_code",
                table: "product_types",
                columns: new[] { "subscriber_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_unit_conversions_product_id",
                table: "product_unit_conversions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_unit_conversions_subscriber_alt_uom",
                table: "product_unit_conversions",
                columns: new[] { "subscriber_id", "alternate_uom_code" });

            migrationBuilder.CreateIndex(
                name: "IX_products_brand_id",
                table: "products",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_excise_account_id",
                table: "products",
                column: "excise_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_ice_code",
                table: "products",
                column: "ice_code");

            migrationBuilder.CreateIndex(
                name: "IX_products_line_id",
                table: "products",
                column: "line_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_product_type_id",
                table: "products",
                column: "product_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_purchase_vat_account_id",
                table: "products",
                column: "purchase_vat_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_purchase_vat_code",
                table: "products",
                column: "purchase_vat_code");

            migrationBuilder.CreateIndex(
                name: "IX_products_sale_vat_account_id",
                table: "products",
                column: "sale_vat_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_sale_vat_code",
                table: "products",
                column: "sale_vat_code");

            migrationBuilder.CreateIndex(
                name: "IX_products_subcategory_id",
                table: "products",
                column: "subcategory_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_subscriber_company",
                table: "products",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_products_subscriber_id",
                table: "products",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_subscriber_sale_code",
                table: "products",
                columns: new[] { "subscriber_id", "sale_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_subscriber_short_name",
                table: "products",
                columns: new[] { "subscriber_id", "short_name" });

            migrationBuilder.CreateIndex(
                name: "IX_products_tariff_id",
                table: "products",
                column: "tariff_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_uom_code",
                table: "products",
                column: "uom_code");

            migrationBuilder.CreateIndex(
                name: "ix_purch_bill_subscriber_status",
                table: "purch_bill",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_purch_bill_subscriber_supplier_status",
                table: "purch_bill",
                columns: new[] { "subscriber_id", "business_partner_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_purch_bill_access_key",
                table: "purch_bill",
                columns: new[] { "subscriber_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purch_bill_supplier_invoice",
                table: "purch_bill",
                columns: new[] { "subscriber_id", "business_partner_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purch_bill_line_bill_id",
                table: "purch_bill_line",
                column: "purch_bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_purch_bill_line_subscriber_product",
                table: "purch_bill_line",
                columns: new[] { "subscriber_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "idx_pid_invoice",
                table: "purch_inv_detail",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_purch_note_bill_id",
                table: "purch_note",
                column: "purch_bill_id");

            migrationBuilder.CreateIndex(
                name: "IX_purch_note_business_partner_id",
                table: "purch_note",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_purch_note_expense_id",
                table: "purch_note",
                column: "expense_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_purch_note_subscriber_supplier_status",
                table: "purch_note",
                columns: new[] { "subscriber_id", "business_partner_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_purch_note_access_key",
                table: "purch_note",
                columns: new[] { "subscriber_id", "access_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purch_note_line_note_id",
                table: "purch_note_line",
                column: "purch_note_id");

            migrationBuilder.CreateIndex(
                name: "ix_purch_retention_line_retention_id",
                table: "purch_retention_line",
                column: "issued_retention_id");

            migrationBuilder.CreateIndex(
                name: "ix_purch_warehouse_alloc_bill_id",
                table: "purch_warehouse_alloc",
                columns: new[] { "subscriber_id", "purch_bill_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_detail_document",
                table: "purchase_detail",
                column: "purchase_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_document_ReferenceDocumentId",
                table: "purchase_document",
                column: "ReferenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "idx_pi_company",
                table: "purchase_invoice",
                columns: new[] { "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_pi_date",
                table: "purchase_invoice",
                columns: new[] { "company_id", "invoice_date" });

            migrationBuilder.CreateIndex(
                name: "idx_pi_supplier",
                table: "purchase_invoice",
                columns: new[] { "company_id", "business_partner_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_tax_support_code",
                table: "purchase_invoice",
                column: "tax_support_code");

            migrationBuilder.CreateIndex(
                name: "uq_pi_key",
                table: "purchase_invoice",
                columns: new[] { "company_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_pi_number",
                table: "purchase_invoice",
                columns: new[] { "company_id", "business_partner_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_subscriber_status",
                table: "purchase_order",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_subscriber_supplier",
                table: "purchase_order",
                columns: new[] { "subscriber_id", "business_partner_id" });

            migrationBuilder.CreateIndex(
                name: "uq_purchase_order_number",
                table: "purchase_order",
                columns: new[] { "subscriber_id", "order_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_purchase_order_bill",
                table: "purchase_order_bill",
                columns: new[] { "purchase_order_id", "purch_bill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_line_order_id",
                table: "purchase_order_line",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_withholding_business_partner_id",
                table: "purchase_withholding",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_withholding_purchase_document_id",
                table: "purchase_withholding",
                column: "purchase_document_id");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_withholding_subscriber_access_key",
                table: "purchase_withholding",
                columns: new[] { "subscriber_id", "access_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_withholding_line_purchase_withholding_id",
                table: "purchase_withholding_line",
                column: "purchase_withholding_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_subscriber_business_partner",
                table: "quote",
                columns: new[] { "subscriber_id", "business_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_quote_subscriber_issue_date",
                table: "quote",
                columns: new[] { "subscriber_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_quote_subscriber_status",
                table: "quote",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_quote_number",
                table: "quote",
                columns: new[] { "subscriber_id", "branch_id", "quote_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quote_detail_quote_id",
                table: "quote_detail",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "uq_quote_detail_line",
                table: "quote_detail",
                columns: new[] { "subscriber_id", "quote_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quote_status_history_quote",
                table: "quote_status_history",
                columns: new[] { "subscriber_id", "quote_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_status_history_quote_id",
                table: "quote_status_history",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "IX_received_wh_detail_withholding_id",
                table: "received_wh_detail",
                column: "withholding_id");

            migrationBuilder.CreateIndex(
                name: "idx_rw_company",
                table: "received_withholding",
                columns: new[] { "company_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "idx_rw_customer",
                table: "received_withholding",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "uq_rw_key",
                table: "received_withholding",
                columns: new[] { "company_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires_at",
                table: "refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_family_id",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_subscriber",
                table: "refresh_tokens",
                columns: new[] { "user_id", "subscriber_id" });

            migrationBuilder.CreateIndex(
                name: "idx_retry_next",
                table: "retry_control",
                columns: new[] { "company_id", "next_retry_at" },
                filter: "is_exhausted = false");

            migrationBuilder.CreateIndex(
                name: "uq_retry_doc",
                table: "retry_control",
                column: "doc_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saas_billing_events_subscriber_occurred",
                table: "saas_billing_events",
                columns: new[] { "subscriber_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_saas_billing_invoice_lines_invoice",
                table: "saas_billing_invoice_lines",
                column: "billing_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ux_saas_billing_invoices_subscriber_number",
                table: "saas_billing_invoices",
                columns: new[] { "subscriber_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_bill_business_partner_id",
                table: "sales_bill",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_bill_subscriber_company",
                table: "sales_bill",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_bill_subscriber_date",
                table: "sales_bill",
                columns: new[] { "subscriber_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_bill_warehouse_id",
                table: "sales_bill",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "uq_sales_bill_seq",
                table: "sales_bill",
                columns: new[] { "subscriber_id", "estab_code", "em_point_code", "sequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_bill_line_sales_bill_id",
                table: "sales_bill_line",
                column: "sales_bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_bill_line_subscriber_bill",
                table: "sales_bill_line",
                columns: new[] { "subscriber_id", "sales_bill_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_detail_document",
                table: "sales_detail",
                column: "sales_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_document_business_partner_id",
                table: "sales_document",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_document_reference_document_id",
                table: "sales_document",
                column: "reference_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_document_subscriber_company",
                table: "sales_document",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_document_subscriber_customer_date",
                table: "sales_document",
                columns: new[] { "subscriber_id", "business_partner_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_document_subscriber_date_status_type",
                table: "sales_document",
                columns: new[] { "subscriber_id", "issue_date", "status", "doc_type" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_document_warehouse_id",
                table: "sales_document",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "uq_sales_document_subscriber_access_key",
                table: "sales_document",
                columns: new[] { "subscriber_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_sales_electronic_doc_access_key",
                table: "sales_electronic_doc",
                column: "access_key",
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_si_buyer_id",
                table: "sales_invoice",
                columns: new[] { "company_id", "buyer_id_number" });

            migrationBuilder.CreateIndex(
                name: "idx_si_company",
                table: "sales_invoice",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "idx_si_customer",
                table: "sales_invoice",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_buyer_id_type",
                table: "sales_invoice",
                column: "buyer_id_type");

            migrationBuilder.CreateIndex(
                name: "ix_sales_note_subscriber_bill",
                table: "sales_note",
                columns: new[] { "subscriber_id", "original_bill_id" });

            migrationBuilder.CreateIndex(
                name: "uq_sales_note_seq",
                table: "sales_note",
                columns: new[] { "subscriber_id", "estab_code", "em_point_code", "sequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_note_line_sales_note_id",
                table: "sales_note_line",
                column: "sales_note_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_note_line_subscriber_note",
                table: "sales_note_line",
                columns: new[] { "subscriber_id", "sales_note_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_subscriber_business_partner",
                table: "sales_order",
                columns: new[] { "subscriber_id", "business_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_subscriber_issue_date",
                table: "sales_order",
                columns: new[] { "subscriber_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_subscriber_status",
                table: "sales_order",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_sales_order_number",
                table: "sales_order",
                columns: new[] { "subscriber_id", "branch_id", "order_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_detail_sales_order_id",
                table: "sales_order_detail",
                column: "sales_order_id");

            migrationBuilder.CreateIndex(
                name: "uq_sales_order_detail_line",
                table: "sales_order_detail",
                columns: new[] { "subscriber_id", "sales_order_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_status_history_order",
                table: "sales_order_status_history",
                columns: new[] { "subscriber_id", "sales_order_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_status_history_sales_order_id",
                table: "sales_order_status_history",
                column: "sales_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_payment_document",
                table: "sales_payment",
                column: "sales_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_retention_business_partner_id",
                table: "sales_retention",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_retention_sales_bill_id",
                table: "sales_retention",
                column: "sales_bill_id");

            migrationBuilder.CreateIndex(
                name: "uq_sales_retention_access_key",
                table: "sales_retention",
                columns: new[] { "subscriber_id", "access_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_retention_line_retention_id",
                table: "sales_retention_line",
                column: "sales_retention_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_withholding_business_partner_id",
                table: "sales_withholding",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_withholding_sales_document_id",
                table: "sales_withholding",
                column: "sales_document_id");

            migrationBuilder.CreateIndex(
                name: "uq_sales_withholding_subscriber_access_key",
                table: "sales_withholding",
                columns: new[] { "subscriber_id", "access_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_withholding_line_sales_withholding_id",
                table: "sales_withholding_line",
                column: "sales_withholding_id");

            migrationBuilder.CreateIndex(
                name: "ux_security_admin_scopes_subject",
                table: "security_admin_scope_assignments",
                columns: new[] { "subscriber_id", "subject_type", "subject_key", "scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_sri_ret_code",
                schema: "global",
                table: "sri_retention_code",
                columns: new[] { "tax_type", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_sri_settings_company_id",
                table: "sri_settings",
                column: "company_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_subscriber_company",
                table: "stock_adjustment",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_subscriber_status",
                table: "stock_adjustment",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_subscriber_warehouse",
                table: "stock_adjustment",
                columns: new[] { "subscriber_id", "warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "uq_stock_adjustment_number",
                table: "stock_adjustment",
                columns: new[] { "subscriber_id", "adjustment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_line_adjustment",
                table: "stock_adjustment_line",
                column: "stock_adjustment_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_line_adjustment_sort",
                table: "stock_adjustment_line",
                columns: new[] { "stock_adjustment_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movement_source_doc",
                table: "stock_movement",
                column: "source_doc_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movement_subscriber_product_warehouse",
                table: "stock_movement",
                columns: new[] { "subscriber_id", "product_id", "warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movement_subscriber_type",
                table: "stock_movement",
                columns: new[] { "subscriber_id", "movement_type" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_expiry",
                table: "stock_reservations",
                columns: new[] { "subscriber_id", "expires_at", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_order",
                table: "stock_reservations",
                columns: new[] { "subscriber_id", "order_id" },
                filter: "order_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_product_warehouse_status",
                table: "stock_reservations",
                columns: new[] { "subscriber_id", "product_id", "warehouse_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_source_warehouse_id",
                table: "stock_transfer",
                column: "source_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfer_subscriber_company",
                table: "stock_transfer",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfer_subscriber_status",
                table: "stock_transfer",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_target_warehouse_id",
                table: "stock_transfer",
                column: "target_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "uq_stock_transfer_number",
                table: "stock_transfer",
                columns: new[] { "subscriber_id", "transfer_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_line_stock_transfer_id",
                table: "stock_transfer_line",
                column: "stock_transfer_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfer_line_subscriber_transfer",
                table: "stock_transfer_line",
                columns: new[] { "subscriber_id", "stock_transfer_id" });

            migrationBuilder.CreateIndex(
                name: "ux_subscriber_billing_accounts_subscriber",
                table: "subscriber_billing_accounts",
                column: "subscriber_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_subscriber_billing_profile_subscriber",
                table: "subscriber_billing_profile",
                column: "subscriber_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_subscriber_custom_menus_subscriber",
                table: "subscriber_custom_menus",
                column: "subscriber_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscriber_subscription_events_subscriber_occurred",
                table: "subscriber_subscription_events",
                columns: new[] { "subscriber_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_subscriber_subscriptions_subscriber",
                table: "subscriber_subscriptions",
                column: "subscriber_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscribers_slug",
                table: "subscribers",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_subscription_feature_override_sub_feature",
                table: "subscription_feature_overrides",
                columns: new[] { "subscription_id", "feature_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_subscription_usages_period",
                table: "subscription_usages",
                columns: new[] { "subscriber_id", "feature_id", "period_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_note_invoice_id",
                table: "supplier_note",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "uq_sup_note",
                table: "supplier_note",
                columns: new[] { "company_id", "business_partner_id", "note_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_note_detail_note_id",
                table: "supplier_note_detail",
                column: "note_id");

            migrationBuilder.CreateIndex(
                name: "ix_tariffs_subscriber_code",
                table: "tariffs",
                columns: new[] { "subscriber_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_groups_code",
                table: "ui_nav_groups",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_items_group_id_parent_item_id_sort_order",
                table: "ui_nav_items",
                columns: new[] { "group_id", "parent_item_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_items_group_id_route_path",
                table: "ui_nav_items",
                columns: new[] { "group_id", "route_path" });

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_items_parent_item_id",
                table: "ui_nav_items",
                column: "parent_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_items_saas_feature_definition_id",
                table: "ui_nav_items",
                column: "saas_feature_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_activity_subscriber_entity_created_at",
                table: "user_activity",
                columns: new[] { "subscriber_id", "entity_type", "entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_activity_subscriber_module_created_at",
                table: "user_activity",
                columns: new[] { "subscriber_id", "module", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_activity_subscriber_user_created_at",
                table: "user_activity",
                columns: new[] { "subscriber_id", "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_vat_refund_company",
                table: "vat_refund",
                columns: new[] { "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_branch_id",
                table: "warehouse",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_subscriber_company",
                table: "warehouse",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_warehouse_subscriber_name",
                table: "warehouse",
                columns: new[] { "subscriber_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_wc_supplier",
                table: "withholding_cert",
                columns: new[] { "company_id", "business_partner_id" });

            migrationBuilder.CreateIndex(
                name: "idx_wh_det_doc",
                table: "withholding_detail",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_withholding_detail_tax_support_code",
                table: "withholding_detail",
                column: "tax_support_code");

            migrationBuilder.CreateIndex(
                name: "IX_withholding_detail_WithholdingCertificateId",
                table: "withholding_detail",
                column: "WithholdingCertificateId");

            migrationBuilder.CreateIndex(
                name: "idx_wslog_company",
                table: "ws_log",
                columns: new[] { "company_id", "called_at" });

            migrationBuilder.CreateIndex(
                name: "idx_wslog_doc",
                table: "ws_log",
                column: "doc_id",
                filter: "doc_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_profile_permissions");

            migrationBuilder.DropTable(
                name: "access_profiles");

            migrationBuilder.DropTable(
                name: "accounting_periods");

            migrationBuilder.DropTable(
                name: "accounting_setup");

            migrationBuilder.DropTable(
                name: "ap_entries");

            migrationBuilder.DropTable(
                name: "app_features");

            migrationBuilder.DropTable(
                name: "ar_entries");

            migrationBuilder.DropTable(
                name: "bank_transaction");

            migrationBuilder.DropTable(
                name: "billing_checkout_sessions");

            migrationBuilder.DropTable(
                name: "billing_payment_attempts");

            migrationBuilder.DropTable(
                name: "carriers");

            migrationBuilder.DropTable(
                name: "cash_count");

            migrationBuilder.DropTable(
                name: "commercial_plan_features");

            migrationBuilder.DropTable(
                name: "commercial_plan_limits");

            migrationBuilder.DropTable(
                name: "company_user_memberships");

            migrationBuilder.DropTable(
                name: "config_feature");

            migrationBuilder.DropTable(
                name: "config_global");

            migrationBuilder.DropTable(
                name: "config_module");

            migrationBuilder.DropTable(
                name: "credit_note");

            migrationBuilder.DropTable(
                name: "current_stock");

            migrationBuilder.DropTable(
                name: "debit_note");

            migrationBuilder.DropTable(
                name: "delivery_detail");

            migrationBuilder.DropTable(
                name: "delivery_guide");

            migrationBuilder.DropTable(
                name: "digital_certificate");

            migrationBuilder.DropTable(
                name: "doc_payment");

            migrationBuilder.DropTable(
                name: "doc_tax");

            migrationBuilder.DropTable(
                name: "document_relation");

            migrationBuilder.DropTable(
                name: "document_sequence");

            migrationBuilder.DropTable(
                name: "edoc_invoice_detail");

            migrationBuilder.DropTable(
                name: "expense_category");

            migrationBuilder.DropTable(
                name: "expense_detail");

            migrationBuilder.DropTable(
                name: "first_run_setup_state");

            migrationBuilder.DropTable(
                name: "general_parameter");

            migrationBuilder.DropTable(
                name: "identity_users");

            migrationBuilder.DropTable(
                name: "invoice_detail");

            migrationBuilder.DropTable(
                name: "invoice_electronic");

            migrationBuilder.DropTable(
                name: "invoice_status_history");

            migrationBuilder.DropTable(
                name: "journal_entry_lines");

            migrationBuilder.DropTable(
                name: "kardex_report");

            migrationBuilder.DropTable(
                name: "kardex_snapshot");

            migrationBuilder.DropTable(
                name: "master_bp_carrier_configs");

            migrationBuilder.DropTable(
                name: "master_bp_contacts");

            migrationBuilder.DropTable(
                name: "master_bp_supplier_configs");

            migrationBuilder.DropTable(
                name: "master_company_bp_trading_settings");

            migrationBuilder.DropTable(
                name: "note_detail");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "payment_applications");

            migrationBuilder.DropTable(
                name: "payment_provider_customers");

            migrationBuilder.DropTable(
                name: "payment_provider_subscriptions");

            migrationBuilder.DropTable(
                name: "petty_cash_expense");

            migrationBuilder.DropTable(
                name: "platform_audit_logs");

            migrationBuilder.DropTable(
                name: "platform_provisioning_audit");

            migrationBuilder.DropTable(
                name: "platform_provisioning_lock");

            migrationBuilder.DropTable(
                name: "processed_webhook_events");

            migrationBuilder.DropTable(
                name: "product_barcodes");

            migrationBuilder.DropTable(
                name: "product_colors");

            migrationBuilder.DropTable(
                name: "product_custom_fields");

            migrationBuilder.DropTable(
                name: "product_dimensions");

            migrationBuilder.DropTable(
                name: "product_features");

            migrationBuilder.DropTable(
                name: "product_images");

            migrationBuilder.DropTable(
                name: "product_sizes");

            migrationBuilder.DropTable(
                name: "product_substitutes");

            migrationBuilder.DropTable(
                name: "product_supplier_codes");

            migrationBuilder.DropTable(
                name: "product_tariff_details");

            migrationBuilder.DropTable(
                name: "product_unit_conversions");

            migrationBuilder.DropTable(
                name: "purch_bill_line");

            migrationBuilder.DropTable(
                name: "purch_inv_detail");

            migrationBuilder.DropTable(
                name: "purch_note_line");

            migrationBuilder.DropTable(
                name: "purch_retention_line");

            migrationBuilder.DropTable(
                name: "purch_warehouse_alloc");

            migrationBuilder.DropTable(
                name: "purchase_detail");

            migrationBuilder.DropTable(
                name: "purchase_electronic_doc");

            migrationBuilder.DropTable(
                name: "purchase_order_bill");

            migrationBuilder.DropTable(
                name: "purchase_order_line");

            migrationBuilder.DropTable(
                name: "purchase_settlement");

            migrationBuilder.DropTable(
                name: "purchase_withholding_line");

            migrationBuilder.DropTable(
                name: "quote_detail");

            migrationBuilder.DropTable(
                name: "quote_status_history");

            migrationBuilder.DropTable(
                name: "received_wh_detail");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "retry_control");

            migrationBuilder.DropTable(
                name: "saas_billing_events");

            migrationBuilder.DropTable(
                name: "saas_billing_invoice_lines");

            migrationBuilder.DropTable(
                name: "sales_bill_line");

            migrationBuilder.DropTable(
                name: "sales_detail");

            migrationBuilder.DropTable(
                name: "sales_electronic_doc");

            migrationBuilder.DropTable(
                name: "sales_invoice");

            migrationBuilder.DropTable(
                name: "sales_note_line");

            migrationBuilder.DropTable(
                name: "sales_order_detail");

            migrationBuilder.DropTable(
                name: "sales_order_status_history");

            migrationBuilder.DropTable(
                name: "sales_payment");

            migrationBuilder.DropTable(
                name: "sales_retention_line");

            migrationBuilder.DropTable(
                name: "sales_withholding_line");

            migrationBuilder.DropTable(
                name: "security_admin_scope_assignments");

            migrationBuilder.DropTable(
                name: "sri_retention_code",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_settings");

            migrationBuilder.DropTable(
                name: "stock_adjustment_line");

            migrationBuilder.DropTable(
                name: "stock_movement");

            migrationBuilder.DropTable(
                name: "stock_reservations");

            migrationBuilder.DropTable(
                name: "stock_transfer_line");

            migrationBuilder.DropTable(
                name: "subscriber_billing_accounts");

            migrationBuilder.DropTable(
                name: "subscriber_billing_profile");

            migrationBuilder.DropTable(
                name: "subscriber_custom_menus");

            migrationBuilder.DropTable(
                name: "subscriber_subscription_events");

            migrationBuilder.DropTable(
                name: "subscriber_subscriptions");

            migrationBuilder.DropTable(
                name: "subscription_feature_overrides");

            migrationBuilder.DropTable(
                name: "subscription_usages");

            migrationBuilder.DropTable(
                name: "supplier_note_detail");

            migrationBuilder.DropTable(
                name: "ui_nav_items");

            migrationBuilder.DropTable(
                name: "user_activity");

            migrationBuilder.DropTable(
                name: "vat_refund");

            migrationBuilder.DropTable(
                name: "withholding_detail");

            migrationBuilder.DropTable(
                name: "ws_log");

            migrationBuilder.DropTable(
                name: "bank_statement");

            migrationBuilder.DropTable(
                name: "commercial_plans");

            migrationBuilder.DropTable(
                name: "sri_payment_method",
                schema: "global");

            migrationBuilder.DropTable(
                name: "expense_document");

            migrationBuilder.DropTable(
                name: "invoice");

            migrationBuilder.DropTable(
                name: "master_bp_locations");

            migrationBuilder.DropTable(
                name: "master_bp_roles");

            migrationBuilder.DropTable(
                name: "journal_entries");

            migrationBuilder.DropTable(
                name: "petty_cash");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "purch_note");

            migrationBuilder.DropTable(
                name: "issued_retention");

            migrationBuilder.DropTable(
                name: "purchase_order");

            migrationBuilder.DropTable(
                name: "purchase_withholding");

            migrationBuilder.DropTable(
                name: "quote");

            migrationBuilder.DropTable(
                name: "received_withholding");

            migrationBuilder.DropTable(
                name: "saas_billing_invoices");

            migrationBuilder.DropTable(
                name: "sri_id_type",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sales_note");

            migrationBuilder.DropTable(
                name: "sales_order");

            migrationBuilder.DropTable(
                name: "sales_retention");

            migrationBuilder.DropTable(
                name: "sales_withholding");

            migrationBuilder.DropTable(
                name: "stock_adjustment");

            migrationBuilder.DropTable(
                name: "stock_transfer");

            migrationBuilder.DropTable(
                name: "supplier_note");

            migrationBuilder.DropTable(
                name: "platform_features");

            migrationBuilder.DropTable(
                name: "ui_nav_groups");

            migrationBuilder.DropTable(
                name: "withholding_cert");

            migrationBuilder.DropTable(
                name: "bank_account");

            migrationBuilder.DropTable(
                name: "brands");

            migrationBuilder.DropTable(
                name: "product_categories");

            migrationBuilder.DropTable(
                name: "product_lines");

            migrationBuilder.DropTable(
                name: "product_subcategories");

            migrationBuilder.DropTable(
                name: "product_types");

            migrationBuilder.DropTable(
                name: "sri_ice_rate",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_uom",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_vat_rate",
                schema: "global");

            migrationBuilder.DropTable(
                name: "tariffs");

            migrationBuilder.DropTable(
                name: "expense_invoice");

            migrationBuilder.DropTable(
                name: "purch_bill");

            migrationBuilder.DropTable(
                name: "purchase_document");

            migrationBuilder.DropTable(
                name: "sales_bill");

            migrationBuilder.DropTable(
                name: "sales_document");

            migrationBuilder.DropTable(
                name: "purchase_invoice");

            migrationBuilder.DropTable(
                name: "electronic_doc");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "master_business_partners");

            migrationBuilder.DropTable(
                name: "warehouse");

            migrationBuilder.DropTable(
                name: "sri_tax_support",
                schema: "global");

            migrationBuilder.DropTable(
                name: "emission_point");

            migrationBuilder.DropTable(
                name: "sri_doc_type",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_error_code",
                schema: "global");

            migrationBuilder.DropTable(
                name: "establishment");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "company");

            migrationBuilder.DropTable(
                name: "geo_parishes",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_emission_type",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_environment",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_tax_regime",
                schema: "global");

            migrationBuilder.DropTable(
                name: "subscribers");

            migrationBuilder.DropTable(
                name: "geo_cantons",
                schema: "global");

            migrationBuilder.DropTable(
                name: "geo_provinces",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_country",
                schema: "global");
        }
    }
}
