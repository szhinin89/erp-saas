using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialEnterpriseBaseline : Migration
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
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    period_number = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    closed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    parent_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    account_type = table.Column<int>(type: "integer", nullable: false),
                    nature = table.Column<int>(type: "integer", nullable: false),
                    allows_posting = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
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
                name: "attribute_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "barcode_types",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_barcode_types", x => x.code);
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
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brands", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "communication_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    recipient_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    recipient_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body_html = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    body_text = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    priority = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    scheduled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processing_started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    max_retries = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    correlation_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "communication_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subject_template = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    html_template = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    text_template = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "company_financial_destination_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    old_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    new_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    old_is_active = table.Column<bool>(type: "boolean", nullable: true),
                    new_is_active = table.Column<bool>(type: "boolean", nullable: true),
                    old_accounting_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_accounting_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_financial_destination_audit", x => x.id);
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
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "configuration_change_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    entity_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    field_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    old_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    value_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_sensitive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuration_change_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "credit_terms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    mode = table.Column<int>(type: "integer", nullable: false),
                    total_days = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_terms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "current_stocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    reserved_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_stock_value = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_current_stocks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "doc_type",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "electronic_document_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<int>(type: "integer", nullable: false),
                    from_state = table.Column<int>(type: "integer", nullable: true),
                    to_state = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_electronic_document_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "electronic_document_sri_message",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    message_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    additional_info = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_electronic_document_sri_message", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "electronic_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<int>(type: "integer", nullable: false),
                    source_module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_state = table.Column<int>(type: "integer", nullable: false),
                    environment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    authorization_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    authorization_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xml_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    schema_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    xml_draft_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    signed_xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    authorized_xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pre_dead_letter_state = table.Column<int>(type: "integer", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_electronic_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "identity_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    username_normalized = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    email_normalized = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                name: "import_batch_issues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_batch_row_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batch_issues", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "import_batch_rows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    raw_data = table.Column<string>(type: "jsonb", nullable: false),
                    parsed_data = table.Column<string>(type: "jsonb", nullable: true),
                    has_blocking_issue = table.Column<bool>(type: "boolean", nullable: false),
                    is_imported = table.Column<bool>(type: "boolean", nullable: false),
                    created_business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batch_rows", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    auto_create_catalog_values = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    total_rows = table.Column<int>(type: "integer", nullable: false),
                    valid_rows = table.Column<int>(type: "integer", nullable: false),
                    issue_rows = table.Column<int>(type: "integer", nullable: false),
                    warning_rows = table.Column<int>(type: "integer", nullable: false),
                    imported_rows = table.Column<int>(type: "integer", nullable: false),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_adjustment_reasons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    allowed_movement_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    requires_notes = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_adjustment_reasons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_lots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manufacture_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expiration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    initial_qty = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    current_qty = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    receipt_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_lots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_serials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    serial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    acquired_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sold_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    document_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    receipt_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_serials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "issued_withholding_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    withholding_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_retained = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issued_withholding_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "item_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_base_sale_price = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    new_base_sale_price = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "item_category_nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: "/"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_category_nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_item_category_nodes_item_category_nodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "item_category_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_margin_statuses",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    color_token = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_margin_statuses", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "item_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "journal_entry_sequences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entry_sequences", x => x.id);
                    table.CheckConstraint("chk_journal_entry_seq_non_negative", "last_number >= 0");
                });

            migrationBuilder.CreateTable(
                name: "legal_entity_type",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    sri_tax_category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_entity_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "master_customer_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_customer_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_customer_classifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_customer_classifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_customer_credit_ratings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_customer_credit_ratings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_customer_invoice_formats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_customer_invoice_formats", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_customer_loyalty_tiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_customer_loyalty_tiers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_customer_segments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_customer_segments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_payment_terms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    installments = table.Column<int>(type: "integer", nullable: false),
                    days_between_installments = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_payment_terms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_supplier_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_supplier_good_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_good_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_supplier_ratings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_ratings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_supplier_risks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_risks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_supplier_segments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_segments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_supplier_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    storage_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    public_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    owner_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    alt_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_files", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    data_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_settings", x => x.id);
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_methods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requires_reference = table.Column<bool>(type: "boolean", nullable: false),
                    is_credit_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    detail_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "None"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_methods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "posting_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fact_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    debit_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credit_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posting_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "price_list_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_rule_type = table.Column<int>(type: "integer", nullable: true),
                    old_rule_value = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    new_rule_type = table.Column<int>(type: "integer", nullable: true),
                    new_rule_value = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_list_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "price_list_item_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_list_item_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "price_lists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    rule_type = table.Column<int>(type: "integer", nullable: true),
                    rule_value = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_lists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pricing_rule_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_rule_type = table.Column<int>(type: "integer", nullable: false),
                    old_rule_value = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    new_rule_type = table.Column<int>(type: "integer", nullable: false),
                    new_rule_value = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_rule_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_communications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_communications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoice_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    grand_total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoice_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_line_pvp_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_pvp = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    new_pvp = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_line_pvp_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_return_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    return_number = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    grand_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_return_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_return_sequence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_seq = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_return_sequence", x => x.id);
                    table.CheckConstraint("chk_purchase_return_sequence_current_seq_positive", "\"current_seq\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    absolute_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "ride_pdf_document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    electronic_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<int>(type: "integer", nullable: false),
                    source_xml_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    template_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    template_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branding_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    renderer_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ride_specification_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    storage_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    generated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ride_pdf_document", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_return_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    return_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    grand_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_return_audit", x => x.id);
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
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    calculation_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
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
                name: "sri_irbpnr_rate",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    unit_value = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    calculation_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_irbpnr_rate", x => x.code);
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
                    cert_p12_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cert_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cert_file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    cert_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    cert_uploaded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    environment = table.Column<int>(type: "integer", nullable: false),
                    emission_type = table.Column<int>(type: "integer", nullable: false),
                    wsdl_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "sri_supplier_type",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_supplier_type", x => x.code);
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
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    uom_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    previous_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    result_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    sequence_number = table.Column<long>(type: "bigint", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_doc_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_doc_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unit_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    total_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    running_average_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    running_stock_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    serial_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accounting_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_credit_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<int>(type: "integer", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    balance_before = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    status_before = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status_after = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    target_purchase_payable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_purchase_return_id = table.Column<Guid>(type: "uuid", nullable: true),
                    financial_destination_id = table.Column<Guid>(type: "uuid", nullable: true),
                    financial_destination_code_snapshot = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    destination_type_code_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    accounting_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_method_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    external_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credit_audit", x => x.id);
                });

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
                name: "system_provider_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    legal_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ciiu_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_provider_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_setup_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    is_initialized = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    initialized_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    admin_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_first_run = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    setup_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    setup_token_expiry_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_setup_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    preferred_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "es"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
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
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "journal_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    accounting_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    posted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    entry_number = table.Column<int>(type: "integer", nullable: true),
                    original_journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reverse_journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reverse_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_journal_entries_accounting_periods_accounting_period_id",
                        column: x => x.accounting_period_id,
                        principalTable: "accounting_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_journal_entries_original_journal_entry_id",
                        column: x => x.original_journal_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_journal_entries_reverse_journal_entry_id",
                        column: x => x.reverse_journal_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attribute_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    data_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_variant_axis = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_values = table.Column<string>(type: "jsonb", nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_definitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_attribute_definitions_attribute_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "attribute_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "communication_outbox_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    communication_outbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attachment_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    file_storage_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    binary_content = table.Column<byte[]>(type: "bytea", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_outbox_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_communication_outbox_attachments_communication_outbox_commu~",
                        column: x => x.communication_outbox_id,
                        principalTable: "communication_outbox",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_installments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: false),
                    days_offset = table.Column<int>(type: "integer", nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_installments", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_installments_credit_terms_credit_term_id",
                        column: x => x.credit_term_id,
                        principalTable: "credit_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_flow_policy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    creation_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    confirmation_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    authorization_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    pending_document_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    cancellation_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requires_cancellation_reason = table.Column<bool>(type: "boolean", nullable: false),
                    requires_attachment = table.Column<bool>(type: "boolean", nullable: false),
                    requires_supplier = table.Column<bool>(type: "boolean", nullable: false),
                    requires_due_date = table.Column<bool>(type: "boolean", nullable: false),
                    payable_generation_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    accounting_posting_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    inventory_impact_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    notification_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_flow_policy", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_flow_policy_doc_type_document_type_code",
                        column: x => x.document_type_code,
                        principalSchema: "global",
                        principalTable: "doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_batch_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stored_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batch_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_batch_files_import_batches_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequential = table.Column<int>(type: "integer", nullable: false),
                    adjustment_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    reason_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    adjustment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    executed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_adjustments_inventory_adjustment_reasons_reason_id",
                        column: x => x.reason_id,
                        principalTable: "inventory_adjustment_reasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    short_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    item_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observations = table.Column<string>(type: "text", nullable: true),
                    category_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    base_sale_price = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    sale_vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    purchase_vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    excise_tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_for_sale = table.Column<bool>(type: "boolean", nullable: false),
                    max_discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    available_on_web = table.Column<bool>(type: "boolean", nullable: false),
                    available_on_pos = table.Column<bool>(type: "boolean", nullable: false),
                    available_on_mobile = table.Column<bool>(type: "boolean", nullable: false),
                    is_ecommerce_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_favorite = table.Column<bool>(type: "boolean", nullable: false),
                    tracks_stock = table.Column<bool>(type: "boolean", nullable: false),
                    tracks_lot = table.Column<bool>(type: "boolean", nullable: false),
                    tracks_series = table.Column<bool>(type: "boolean", nullable: false),
                    allow_decimal_qty = table.Column<bool>(type: "boolean", nullable: false),
                    allow_decimal_sale = table.Column<bool>(type: "boolean", nullable: false),
                    min_stock_qty = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    max_stock_qty = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_items_brands_brand_id",
                        column: x => x.brand_id,
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_items_item_category_nodes_category_node_id",
                        column: x => x.category_node_id,
                        principalTable: "item_category_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_items_item_types_item_type_id",
                        column: x => x.item_type_id,
                        principalTable: "item_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                    legal_entity_type_code = table.Column<int>(type: "integer", nullable: false),
                    country_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_business_partners", x => x.id);
                    table.UniqueConstraint("uq_mbp_id_subscriber", x => new { x.id, x.tenant_id });
                    table.ForeignKey(
                        name: "FK_master_business_partners_legal_entity_type_legal_entity_typ~",
                        column: x => x.legal_entity_type_code,
                        principalSchema: "global",
                        principalTable: "legal_entity_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "posting_rule_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    posting_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nature = table.Column<int>(type: "integer", nullable: false),
                    amount_kind = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posting_rule_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_posting_rule_lines_posting_rules_posting_rule_id",
                        column: x => x.posting_rule_id,
                        principalTable: "posting_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "price_list_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_list_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_price_list_items_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pricing_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_type = table.Column<int>(type: "integer", nullable: false),
                    rule_value = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_pricing_rules_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "doc_type_sri_map",
                schema: "global",
                columns: table => new
                {
                    doc_type_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    sri_doc_type_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_type_sri_map", x => x.doc_type_code);
                    table.ForeignKey(
                        name: "FK_doc_type_sri_map_doc_type_doc_type_code",
                        column: x => x.doc_type_code,
                        principalSchema: "global",
                        principalTable: "doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_doc_type_sri_map_sri_doc_type_sri_doc_type_code",
                        column: x => x.sri_doc_type_code,
                        principalSchema: "global",
                        principalTable: "sri_doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sri_id_type_usage",
                schema: "global",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdTypeCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    UsageType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_id_type_usage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sri_id_type_usage_sri_id_type_IdTypeCode",
                        column: x => x.IdTypeCode,
                        principalSchema: "global",
                        principalTable: "sri_id_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_identification_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_temporary_tax_identification = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    tax_identification_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    corporate_email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                    extra_legend = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    language_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "es"),
                    legal_rep_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    legal_rep_position = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    legal_rep_id_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    legal_rep_email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    legal_rep_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    onboarding_completed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    operational_status = table.Column<int>(type: "integer", nullable: false, defaultValue: 2)
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
                        name: "FK_company_sri_tax_regime_tax_regime_code",
                        column: x => x.tax_regime_code,
                        principalSchema: "global",
                        principalTable: "sri_tax_regime",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_custom_menus",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    menu_config = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_custom_menus", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_custom_menus_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ui_nav_items", x => x.Id);
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
                name: "journal_entry_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    debit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    credit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entry_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_adjustment_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_adjustment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    packaging_level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    base_uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    conversion_factor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    quantity_in_base_uom = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_cost_base = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    total_cost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    current_stock_before = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    current_stock_after = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    line_notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_adjustment_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_adjustment_lines_stock_adjustments_stock_adjustment_id",
                        column: x => x.stock_adjustment_id,
                        principalTable: "stock_adjustments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    storage_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alt_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_main = table.Column<bool>(type: "boolean", nullable: false),
                    is_ecommerce = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_images_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_packaging_levels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    base_quantity = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    weight = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    is_base_unit = table.Column<bool>(type: "boolean", nullable: false),
                    is_purchase_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_sale_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_packaging_levels", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_packaging_levels_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_special_tax_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sri_tax_category_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_catalog_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_special_tax_configurations", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_special_tax_configurations_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_substitutes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    substitute_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_substitutes", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_substitutes_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_unit_conversions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    to_uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    factor = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_unit_conversions", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_unit_conversions_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_variants", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_variants_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
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
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_bp_roles", x => x.id);
                    table.UniqueConstraint("uq_bpr_id_subscriber", x => new { x.id, x.tenant_id });
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
                    payment_term_id = table.Column<Guid>(type: "uuid", nullable: true),
                    installments = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    days_between_installments = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    blocked_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    blocked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    blocked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "company_special_tax_responsibilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sri_tax_category_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_responsible_on_sales = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_special_tax_responsibilities", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_special_tax_responsibilities_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "expense_category_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    accounting_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deductible = table.Column<bool>(type: "boolean", nullable: false),
                    requires_invoice = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_category_nodes", x => x.id);
                    table.CheckConstraint("chk_expense_category_nodes_hierarchy", "(\"level\" = 0 AND \"parent_id\" IS NULL AND \"accounting_account_id\" IS NULL) OR (\"level\" = 1 AND \"parent_id\" IS NOT NULL AND \"accounting_account_id\" IS NULL) OR (\"level\" = 2 AND \"parent_id\" IS NOT NULL AND \"accounting_account_id\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_expense_category_nodes_accounts_accounting_account_id",
                        column: x => x.accounting_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_category_nodes_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_category_nodes_expense_category_nodes_parent_id",
                        column: x => x.parent_id,
                        principalTable: "expense_category_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_supplier_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    packaging_level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_supplier_codes", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_supplier_codes_item_packaging_levels_packaging_level_id",
                        column: x => x.packaging_level_id,
                        principalTable: "item_packaging_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_supplier_codes_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_item_supplier_codes_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_variant_attributes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_variant_attributes", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_variant_attributes_item_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "item_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_variant_barcodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    barcode_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_variant_barcodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_variant_barcodes_barcode_types_barcode_type",
                        column: x => x.barcode_type,
                        principalSchema: "global",
                        principalTable: "barcode_types",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_variant_barcodes_item_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "item_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "master_bp_customer_configs",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    customer_segment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sales_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    credit_rating = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    loyalty_tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    preferred_invoice_format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    customer_classification = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_bp_customer_configs", x => x.role_id);
                    table.ForeignKey(
                        name: "fk_bpcrc_role",
                        column: x => x.role_id,
                        principalTable: "master_bp_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "master_bp_supplier_classification_configs",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    supplier_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    supplier_risk = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    supplier_rating = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    primary_good_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    supplier_segment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    payment_method_preference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_bp_supplier_classification_configs", x => x.role_id);
                    table.ForeignKey(
                        name: "fk_bpscc_role",
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
                    payment_terms = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    payment_term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_payment_method_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    refund_provider_type_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    is_retention_exempt = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_required_to_keep_accounting = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
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
                name: "branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_main_branch = table.Column<bool>(type: "boolean", nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_id = table.Column<string>(type: "character(10)", maxLength: 10, nullable: true),
                    province_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    canton_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    parish_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    latitude = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    longitude = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    phone = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    secondary_phone = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    manager_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    manager_position = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    manager_email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    manager_phone = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    opening_date = table.Column<DateOnly>(type: "date", nullable: true),
                    internal_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
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
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "accounts_payables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_type = table.Column<int>(type: "integer", nullable: false),
                    origin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    document_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    accounting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts_payables", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounts_payables_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_payables_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_payables_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_user_branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_user_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_user_branches", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_user_branches_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_user_branches_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_user_branches_company_user_memberships_company_user~",
                        column: x => x.company_user_membership_id,
                        principalTable: "company_user_memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company_user_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_user_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    login_mode = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_user_preferences", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_user_preferences_branches_default_branch_id",
                        column: x => x.default_branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_user_preferences_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_user_preferences_company_user_memberships_company_u~",
                        column: x => x.company_user_membership_id,
                        principalTable: "company_user_memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "establishment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_main = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
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
                name: "expense_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_tax_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    accounting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    document_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    document_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    authorization_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    authorization_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payment_term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_term_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    payment_term_installments = table.Column<int>(type: "integer", nullable: false),
                    payment_term_days_between = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    confirmed_subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    confirmed_total_tax = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    confirmed_total_discount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    confirmed_grand_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_documents_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_documents_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_documents_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_documents_master_payment_terms_payment_term_id",
                        column: x => x.payment_term_id,
                        principalTable: "master_payment_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                    reversed_by = table.Column<Guid>(type: "uuid", nullable: true),
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
                name: "user_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    refresh_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_sessions_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_sessions_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_sessions_identity_users_identity_user_id",
                        column: x => x.identity_user_id,
                        principalTable: "identity_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_sessions_refresh_tokens_refresh_token_id",
                        column: x => x.refresh_token_id,
                        principalTable: "refresh_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "accounts_payable_installments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accounts_payable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    retained_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    return_credit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    supplier_credit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    credit_note_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts_payable_installments", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounts_payable_installments_accounts_payables_accounts_pa~",
                        column: x => x.accounts_payable_id,
                        principalTable: "accounts_payables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                    emission_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
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
                name: "warehouses",
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
                    capacity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    daily_dispatch_goal = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    is_main = table.Column<bool>(type: "boolean", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.id);
                    table.ForeignKey(
                        name: "FK_warehouses_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouses_establishment_establishment_id",
                        column: x => x.establishment_id,
                        principalTable: "establishment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "expense_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_subcategory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_accounting_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_accounting_account_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    snapshot_accounting_account_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_amount = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    snapshot_vat_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_lines_accounts_snapshot_accounting_account_id",
                        column: x => x.snapshot_accounting_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_lines_expense_category_nodes_expense_subcategory_id",
                        column: x => x.expense_subcategory_id,
                        principalTable: "expense_category_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_lines_expense_documents_expense_document_id",
                        column: x => x.expense_document_id,
                        principalTable: "expense_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expense_payment_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_payment_schedules", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_payment_schedules_expense_documents_expense_documen~",
                        column: x => x.expense_document_id,
                        principalTable: "expense_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "document_sequence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    current_seq = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_sequence", x => x.id);
                    table.CheckConstraint("chk_doc_seq_positive", "current_seq >= 1");
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
                name: "cash_registers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    default_warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_registers", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_registers_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_registers_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_registers_emission_point_emission_point_id",
                        column: x => x.emission_point_id,
                        principalTable: "emission_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_registers_master_business_partners_default_customer_id",
                        column: x => x.default_customer_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_registers_warehouses_default_warehouse_id",
                        column: x => x.default_warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    authorization_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    authorization_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tax_support_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    payment_method_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    payment_method_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_tax_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchase_order_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    global_warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_term_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    payment_term_installments = table.Column<int>(type: "integer", nullable: false),
                    payment_term_days_between = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    confirmed_subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    confirmed_total_tax = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    confirmed_total_discount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    confirmed_grand_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_invoices_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoices_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoices_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoices_master_payment_terms_payment_term_id",
                        column: x => x.payment_term_id,
                        principalTable: "master_payment_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoices_warehouses_global_warehouse_id",
                        column: x => x.global_warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequential = table.Column<int>(type: "integer", nullable: false),
                    transfer_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    operation_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfers", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_transfers_branches_operation_branch_id",
                        column: x => x.operation_branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_warehouses_source_warehouse_id",
                        column: x => x.source_warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_warehouses_target_warehouse_id",
                        column: x => x.target_warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cash_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_code_snapshot = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cash_register_name_snapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    emission_point_code_snapshot = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    opening_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    close_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    expected_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    counted_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    difference = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_sessions_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_sessions_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_sessions_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_sessions_emission_point_emission_point_id",
                        column: x => x.emission_point_id,
                        principalTable: "emission_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_financial_destinations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination_type_code = table.Column<int>(type: "integer", nullable: false),
                    accounting_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bank_institution_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bank_account_identifier_normalized = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_financial_destinations", x => x.id);
                    table.CheckConstraint("chk_company_financial_destination_type_fields", "(\"destination_type_code\" = 1 AND \"bank_institution_code\" IS NOT NULL AND \"bank_account_identifier_normalized\" IS NOT NULL AND \"cash_register_id\" IS NULL) OR (\"destination_type_code\" = 2 AND \"cash_register_id\" IS NOT NULL AND \"bank_institution_code\" IS NULL AND \"bank_account_identifier_normalized\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_company_financial_destinations_accounts_accounting_account_~",
                        column: x => x.accounting_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_financial_destinations_cash_registers_cash_register~",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_financial_destinations_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "issued_withholdings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    withholding_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_retained_vat = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_retained_income = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_retained_isd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_retained = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    cancel_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    signed_xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pdf_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sri_status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    sri_receipt_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sri_authorization_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    sri_authorization_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sri_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issued_withholdings", x => x.id);
                    table.ForeignKey(
                        name: "FK_issued_withholdings_emission_point_emission_point_id",
                        column: x => x.emission_point_id,
                        principalTable: "emission_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issued_withholdings_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issued_withholdings_purchase_invoices_purchase_invoice_id",
                        column: x => x.purchase_invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoice_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    snapshot_sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    snapshot_item_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    snapshot_supplier_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    packaging_level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "UNIT"),
                    base_uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "UNIT"),
                    conversion_factor = table.Column<decimal>(type: "numeric(18,6)", nullable: false, defaultValue: 1m),
                    quantity_in_base_uom = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    freight_allocated = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    other_costs_allocated = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_line_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: false, defaultValue: 0m),
                    landed_unit_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: false, defaultValue: 0m),
                    is_frozen = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    snapshot_vat_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    snapshot_warehouse_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    snapshot_item_pvp = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    purchase_order_detail_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ordered_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    purchase_reception_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoice_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_details_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_details_purchase_invoices_purchase_invoice~",
                        column: x => x.purchase_invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_details_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoice_tax_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    vat_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ice_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ice_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ice_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    irbpnr_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    irbpnr_rate = table.Column<decimal>(type: "numeric(10,4)", nullable: false, defaultValue: 0m),
                    irbpnr_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ice_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    irbpnr_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoice_tax_summaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_tax_summaries_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_tax_summaries_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_tax_summaries_purchase_invoices_purchase_i~",
                        column: x => x.purchase_invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_payment_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_payment_schedules", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_payment_schedules_purchase_invoices_purchase_invoi~",
                        column: x => x.purchase_invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_reception_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_doc_type = table.Column<int>(type: "integer", nullable: false),
                    supplier_ruc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    modified_document_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    authorization_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    authorization_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    xml_downloaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    doc_type_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    sri_payment_method_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    processing_status = table.Column<int>(type: "integer", nullable: false),
                    lines_detected_count = table.Column<int>(type: "integer", nullable: false),
                    lines_processed_count = table.Column<int>(type: "integer", nullable: false),
                    processing_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    document_status = table.Column<int>(type: "integer", nullable: false),
                    purchase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xml_content = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_reception_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_reception_documents_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_reception_documents_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_reception_documents_master_business_partners_suppl~",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchase_reception_documents_purchase_invoices_purchase_id",
                        column: x => x.purchase_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_transfer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfer_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_transfer_lines_stock_transfers_stock_transfer_id",
                        column: x => x.stock_transfer_id,
                        principalTable: "stock_transfers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cash_closing_counts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    denomination_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    denomination_label = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_closing_counts", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_closing_counts_cash_sessions_cash_session_id",
                        column: x => x.cash_session_id,
                        principalTable: "cash_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cash_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_movements_cash_sessions_cash_session_id",
                        column: x => x.cash_session_id,
                        principalTable: "cash_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: true),
                    emission_type = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    sri_payment_method_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    customer_tax_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    customer_id_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    customer_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    customer_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    payment_term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_term_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    payment_term_installments = table.Column<int>(type: "integer", nullable: false),
                    payment_term_days_between = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    authorized_subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_total_tax = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_total_discount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_grand_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_invoices_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_invoices_cash_sessions_cash_session_id",
                        column: x => x.cash_session_id,
                        principalTable: "cash_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_invoices_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_invoices_emission_point_emission_point_id",
                        column: x => x.emission_point_id,
                        principalTable: "emission_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sales_invoices_master_business_partners_customer_id",
                        column: x => x.customer_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                    financial_destination_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    applied_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reversed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reverse_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.CheckConstraint("chk_payments_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "FK_payments_company_financial_destinations_financial_destinati~",
                        column: x => x.financial_destination_id,
                        principalTable: "company_financial_destinations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_master_business_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_payment_methods_payment_method_id",
                        column: x => x.payment_method_id,
                        principalTable: "payment_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "issued_withholding_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    withholding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    retention_code_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issued_withholding_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_issued_withholding_details_issued_withholdings_withholding_~",
                        column: x => x.withholding_id,
                        principalTable: "issued_withholdings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoice_detail_taxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_rate_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    calculation_type = table.Column<int>(type: "integer", nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoice_detail_taxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_detail_taxes_purchase_invoice_details_purc~",
                        column: x => x.purchase_invoice_detail_id,
                        principalTable: "purchase_invoice_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_reception_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_reception_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    supplier_aux_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    vat_percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    tax_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ice_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ice_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    discount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    line_subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_line = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    match_status = table.Column<int>(type: "integer", nullable: false),
                    matched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    matched_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_reception_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_reception_lines_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_reception_lines_purchase_reception_documents_purch~",
                        column: x => x.purchase_reception_document_id,
                        principalTable: "purchase_reception_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_returns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_number = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    fiscal_status = table.Column<int>(type: "integer", nullable: false),
                    supplier_credit_note_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    authorized_subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_vat_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_ice_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_discount_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_irbpnr_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_grand_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    historical_cost_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    cost_variance_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    applied_to_payable_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    supplier_credit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    authorized_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    create_client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    create_request_payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    authorize_client_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    authorize_request_payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cancel_client_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancel_request_payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    link_credit_note_client_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    link_credit_note_request_payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_returns", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_returns_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_returns_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_returns_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_returns_purchase_invoices_purchase_invoice_id",
                        column: x => x.purchase_invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_returns_purchase_reception_documents_supplier_cred~",
                        column: x => x.supplier_credit_note_document_id,
                        principalTable: "purchase_reception_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    snapshot_sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    snapshot_item_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    packaging_level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    base_uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    conversion_factor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    quantity_in_base_uom = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    snapshot_vat_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    is_frozen = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_invoice_details_sales_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sales_invoice_details_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payment_method_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_invoice_payments_sales_invoices_sales_invoice_id",
                        column: x => x.sales_invoice_id,
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_receivables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_receivables", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_receivables_master_business_partners_customer_id",
                        column: x => x.customer_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_receivables_sales_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_returns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    credit_note_document_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    authorized_subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_total_vat = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_total_ice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_total_discount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_total_irbpnr = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_grand_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_returns", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_returns_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_returns_master_business_partners_customer_id",
                        column: x => x.customer_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_returns_sales_invoices_sales_invoice_id",
                        column: x => x.sales_invoice_id,
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "purchase_reception_line_additional_fields",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_reception_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    value = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_reception_line_additional_fields", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_reception_line_additional_fields_purchase_receptio~",
                        column: x => x.purchase_reception_line_id,
                        principalTable: "purchase_reception_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_reception_line_taxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_reception_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_rate_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_reception_line_taxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_reception_line_taxes_purchase_reception_lines_purc~",
                        column: x => x.purchase_reception_line_id,
                        principalTable: "purchase_reception_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_credit_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reception_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    application_type = table.Column<int>(type: "integer", nullable: false),
                    linked_purchase_return_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    credit_note_number = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    authorization_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    authorization_date = table.Column<DateOnly>(type: "date", nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ice_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    irbpnr_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    applied_to_payable_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    authorized_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    create_client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    create_request_payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    authorize_client_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    authorize_request_payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cancel_client_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancel_request_payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_credit_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_credit_notes_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_credit_notes_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_credit_notes_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_credit_notes_purchase_invoices_purchase_invoice_id",
                        column: x => x.purchase_invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_credit_notes_purchase_reception_documents_receptio~",
                        column: x => x.reception_document_id,
                        principalTable: "purchase_reception_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_credit_notes_purchase_returns_linked_purchase_retu~",
                        column: x => x.linked_purchase_return_id,
                        principalTable: "purchase_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_return_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_invoice_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    vat_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    returned_subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    returned_discount_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    returned_vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    historical_cost_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    is_frozen = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_return_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_return_details_purchase_invoice_details_original_i~",
                        column: x => x.original_invoice_detail_id,
                        principalTable: "purchase_invoice_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_return_details_purchase_returns_purchase_return_id",
                        column: x => x.purchase_return_id,
                        principalTable: "purchase_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_return_details_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_credits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    source_purchase_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    available_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credits", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_credits_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credits_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credits_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credits_purchase_returns_source_purchase_return_id",
                        column: x => x.source_purchase_return_id,
                        principalTable: "purchase_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice_detail_taxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_invoice_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_rate_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    calculation_type = table.Column<int>(type: "integer", nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice_detail_taxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_invoice_detail_taxes_sales_invoice_details_sales_invo~",
                        column: x => x.sales_invoice_detail_id,
                        principalTable: "sales_invoice_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_card_details",
                columns: table => new
                {
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_brand = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    card_last_four = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    authorization_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    lot_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_card_details", x => x.payment_id);
                    table.ForeignKey(
                        name: "FK_payment_card_details_sales_invoice_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "sales_invoice_payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_cheque_details",
                columns: table => new
                {
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cheque_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    holder_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cash_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_cheque_details", x => x.payment_id);
                    table.ForeignKey(
                        name: "FK_payment_cheque_details_sales_invoice_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "sales_invoice_payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_transfer_details",
                columns: table => new
                {
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    receipt_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    transfer_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transfer_details", x => x.payment_id);
                    table.ForeignKey(
                        name: "FK_payment_transfer_details_sales_invoice_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "sales_invoice_payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    sort_order = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_application_lines", x => x.id);
                    table.CheckConstraint("chk_payment_application_line_applied_amount_positive", "applied_amount > 0");
                    table.CheckConstraint("chk_payment_application_line_document_xor", "(receivable_id IS NOT NULL AND payable_id IS NULL) OR (receivable_id IS NULL AND payable_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_payment_application_lines_accounts_payables_payable_id",
                        column: x => x.payable_id,
                        principalTable: "accounts_payables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_application_lines_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_application_lines_sales_receivables_receivable_id",
                        column: x => x.receivable_id,
                        principalTable: "sales_receivables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_receivable_installments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receivable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_receivable_installments", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_receivable_installments_sales_receivables_receivable_~",
                        column: x => x.receivable_id,
                        principalTable: "sales_receivables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_return_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_invoice_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    snapshot_sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    snapshot_item_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    packaging_level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    base_uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    conversion_factor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    quantity_in_base_uom = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_frozen = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_return_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_return_details_sales_invoice_details_original_invoice~",
                        column: x => x.original_invoice_detail_id,
                        principalTable: "sales_invoice_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_return_details_sales_returns_return_id",
                        column: x => x.return_id,
                        principalTable: "sales_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sales_return_details_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_return_refund_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_return_refund_allocations", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_return_refund_allocations_sales_returns_return_id",
                        column: x => x.return_id,
                        principalTable: "sales_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_credit_note_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_credit_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vat_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_credit_note_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_credit_note_details_purchase_credit_notes_purchase~",
                        column: x => x.purchase_credit_note_id,
                        principalTable: "purchase_credit_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_credit_note_tax_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_credit_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_purchase_invoice_tax_summary_id = table.Column<Guid>(type: "uuid", nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_credit_note_tax_summaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_credit_note_tax_summaries_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_credit_note_tax_summaries_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_credit_note_tax_summaries_purchase_credit_notes_pu~",
                        column: x => x.purchase_credit_note_id,
                        principalTable: "purchase_credit_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_credit_note_tax_summaries_purchase_invoice_tax_sum~",
                        column: x => x.source_purchase_invoice_tax_summary_id,
                        principalTable: "purchase_invoice_tax_summaries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_credit_note_tax_summaries_purchase_invoices_purcha~",
                        column: x => x.purchase_invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_return_detail_taxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_return_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_rate_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    calculation_type = table.Column<int>(type: "integer", nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_return_detail_taxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_return_detail_taxes_purchase_return_details_purcha~",
                        column: x => x.purchase_return_detail_id,
                        principalTable: "purchase_return_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_credit_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_credit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    target_purchase_payable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversal_of_movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credit_movements", x => x.id);
                    table.CheckConstraint("chk_supplier_credit_movement_amount_positive", "\"amount\" > 0");
                    table.CheckConstraint("chk_supplier_credit_movement_reversal_ref", "(\"movement_type\" IN (3, 4) AND \"reversal_of_movement_id\" IS NOT NULL) OR (\"movement_type\" NOT IN (3, 4) AND \"reversal_of_movement_id\" IS NULL)");
                    table.CheckConstraint("chk_supplier_credit_movement_target_payable", "(\"movement_type\" IN (1, 3) AND \"target_purchase_payable_id\" IS NOT NULL) OR (\"movement_type\" NOT IN (1, 3) AND \"target_purchase_payable_id\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_supplier_credit_movements_accounts_payables_target_purchase~",
                        column: x => x.target_purchase_payable_id,
                        principalTable: "accounts_payables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_movements_supplier_credit_movements_reversa~",
                        column: x => x.reversal_of_movement_id,
                        principalTable: "supplier_credit_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_movements_supplier_credits_supplier_credit_~",
                        column: x => x.supplier_credit_id,
                        principalTable: "supplier_credits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_return_detail_taxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_return_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_rate_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    calculation_type = table.Column<int>(type: "integer", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_return_detail_taxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_return_detail_taxes_sales_return_details_sales_return~",
                        column: x => x.sales_return_detail_id,
                        principalTable: "sales_return_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_credit_note_tax_summary_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_credit_note_tax_summary_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_rate_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    calculation_type = table.Column<int>(type: "integer", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_credit_note_tax_summary_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_credit_note_tax_summary_lines_purchase_credit_note~",
                        column: x => x.purchase_credit_note_tax_summary_id,
                        principalTable: "purchase_credit_note_tax_summaries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_credit_refund_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_credit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_credit_movement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_type_code = table.Column<int>(type: "integer", nullable: false),
                    original_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    financial_destination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accounting_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    external_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cash_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    financial_destination_code_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    financial_destination_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination_type_code_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    accounting_account_code_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credit_refund_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_accounts_accounting_acc~",
                        column: x => x.accounting_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_cash_movements_cash_mov~",
                        column: x => x.cash_movement_id,
                        principalTable: "cash_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_cash_sessions_cash_sess~",
                        column: x => x.cash_session_id,
                        principalTable: "cash_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_company_financial_desti~",
                        column: x => x.financial_destination_id,
                        principalTable: "company_financial_destinations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_supplier_credit_movemen~",
                        column: x => x.supplier_credit_movement_id,
                        principalTable: "supplier_credit_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_supplier_credit_refund_~",
                        column: x => x.original_transaction_id,
                        principalTable: "supplier_credit_refund_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_supplier_credits_suppli~",
                        column: x => x.supplier_credit_id,
                        principalTable: "supplier_credits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "barcode_types",
                columns: new[] { "code", "is_active", "name" },
                values: new object[,]
                {
                    { "Code128", true, "Code 128" },
                    { "EAN13", true, "EAN-13" },
                    { "EAN8", true, "EAN-8" },
                    { "Internal", true, "Interno" },
                    { "Other", true, "Otro" },
                    { "QR", true, "QR" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "doc_type",
                columns: new[] { "code", "is_active", "name" },
                values: new object[,]
                {
                    { "AJUINV", true, "Ajuste de Inventario" },
                    { "ASI", true, "Asiento Contable Manual" },
                    { "COBCLI", true, "Cobro a Cliente" },
                    { "FACCOM", true, "Factura de Compra" },
                    { "FACVEN", true, "Factura de Venta" },
                    { "GASDOC", true, "Documento de Gasto" },
                    { "NCCDEV", true, "Nota de Crédito de Compra" },
                    { "NCVDEV", true, "Nota de Crédito de Venta" },
                    { "PAGPRO", true, "Pago a Proveedor" },
                    { "RETGAS", true, "Retención en Gasto" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "item_margin_statuses",
                columns: new[] { "code", "color_token", "label" },
                values: new object[,]
                {
                    { "BAJO", "warning", "Bajo" },
                    { "CERO", "neutral", "Sin margen" },
                    { "NEGATIVO", "error", "Negativo" },
                    { "SALUDABLE", "success", "Saludable" },
                    { "SIN_PRECIO", "neutral", "Sin precio" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "legal_entity_type",
                columns: new[] { "code", "is_active", "name", "sri_tax_category" },
                values: new object[,]
                {
                    { 1, true, "Persona Natural", "NATURAL" },
                    { 2, true, "Sociedad Privada", "PRIVATE" },
                    { 3, true, "Institución Pública", "PUBLIC" }
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
                    { "CAN", true, "CA", "CANADÁ", "+1" },
                    { "CHL", true, "CL", "CHILE", "+56" },
                    { "CHN", true, "CN", "CHINA", "+86" },
                    { "COL", true, "CO", "COLOMBIA", "+57" },
                    { "CRI", true, "CR", "COSTA RICA", "+506" },
                    { "DEU", true, "DE", "ALEMANIA", "+49" },
                    { "DOM", true, "DO", "REPÚBLICA DOMINICANA", "+1" },
                    { "ECU", true, "EC", "ECUADOR", "+593" },
                    { "ESP", true, "ES", "ESPAÑA", "+34" },
                    { "FRA", true, "FR", "FRANCIA", "+33" },
                    { "GBR", true, "GB", "REINO UNIDO", "+44" },
                    { "GTM", true, "GT", "GUATEMALA", "+502" },
                    { "HND", true, "HN", "HONDURAS", "+504" },
                    { "IND", true, "IN", "INDIA", "+91" },
                    { "ITA", true, "IT", "ITALIA", "+39" },
                    { "JPN", true, "JP", "JAPÓN", "+81" },
                    { "MEX", true, "MX", "MÉXICO", "+52" },
                    { "NIC", true, "NI", "NICARAGUA", "+505" },
                    { "PAN", true, "PA", "PANAMÁ", "+507" },
                    { "PER", true, "PE", "PERÚ", "+51" },
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
                values: new object[] { "01", true, true, "FACTURA", "FACTURA" });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_doc_type",
                columns: new[] { "code", "is_electronic", "name", "short_name" },
                values: new object[] { "02", true, "NOTA DE VENTA- RISE", "NV_RISE" });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_doc_type",
                columns: new[] { "code", "is_active", "is_electronic", "name", "short_name" },
                values: new object[,]
                {
                    { "03", true, true, "Liquidación de Compra de Bienes y Prestación de Servicios", "LIQ_COMPRA" },
                    { "04", true, true, "Nota de Crédito", "N_CREDITO" },
                    { "05", true, true, "Nota de Débito", "N_DEBITO" },
                    { "06", true, true, "Guía de Remisión", "G_REMISION" },
                    { "07", true, true, "Comprobante de Retención", "retention" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_doc_type",
                columns: new[] { "code", "name", "short_name" },
                values: new object[,]
                {
                    { "08", "Tiquete de Máquina Registradora", "TIQUETE" },
                    { "09", "Tiquete de Caja Registradora", "CAJA_REG" },
                    { "18", "Documento Electrónico de Importación", "DEI" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_emission_type",
                columns: new[] { "code", "name" },
                values: new object[,]
                {
                    { (short)1, "Emisión Normal" },
                    { (short)2, "Emisión por Indisponibilidad del Sistema" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_error_code",
                columns: new[] { "code", "description", "error_type", "is_active", "name" },
                values: new object[,]
                {
                    { "27", null, "ERROR", true, "CLASE NO PERMITIDO" },
                    { "28", null, "ERROR", true, "ACUERDO DE MEDIOS ELECTRÓNICOS NO ACEPTADO" },
                    { "34", null, "ERROR", true, "COMPROBANTE NO AUTORIZADO" },
                    { "35", null, "ERROR", true, "DOCUMENTO INVÁLIDO" },
                    { "36", null, "ERROR", true, "VERSIÓN ESQUEMA DESCONTINUADA" },
                    { "37", null, "ERROR", true, "RUC SIN AUTORIZACIÓN DE EMISIÓN" },
                    { "39", null, "ERROR", true, "FIRMA INVÁLIDA" },
                    { "40", null, "ERROR", true, "ERROR EN EL CERTIFICADO" },
                    { "42", null, "ERROR", true, "CERTIFICADO REVOCADO" },
                    { "43", null, "ERROR", true, "CLAVE ACCESO REGISTRADA" },
                    { "45", null, "ERROR", true, "SECUENCIAL REGISTRADO" },
                    { "46", null, "ERROR", true, "RUC NO EXISTE" },
                    { "47", null, "ERROR", true, "TIPO DE COMPROBANTE NO EXISTE" },
                    { "48", null, "ERROR", true, "ESQUEMA XSD NO EXISTE" },
                    { "49", null, "ERROR", true, "ARGUMENTOS QUE ENVÍAN AL WS NULOS" },
                    { "50", null, "ERROR", true, "ERROR INTERNO GENERAL" },
                    { "52", null, "ERROR", true, "ERROR EN DIFERENCIAS" },
                    { "56", null, "ERROR", true, "ESTABLECIMIENTO CERRADO" },
                    { "57", null, "ERROR", true, "AUTORIZACIÓN SUSPENDIDA" },
                    { "58", null, "ERROR", true, "ERROR EN LA ESTRUCTURA DE CLAVE ACCESO" },
                    { "59", null, "WARNING", true, "IDENTIFICACIÓN NO EXISTE" },
                    { "60", null, "WARNING", true, "AMBIENTE EJECUCIÓN" },
                    { "62", null, "WARNING", true, "IDENTIFICACIÓN INCORRECTA" },
                    { "63", null, "ERROR", true, "RUC CLAUSURADO" },
                    { "64", null, "ERROR", true, "CÓDIGO DOCUMENTO SUSTENTO" },
                    { "65", null, "ERROR", true, "FECHA DE EMISIÓN EXTEMPORÁNEA" },
                    { "67", null, "ERROR", true, "FECHA INVÁLIDA" },
                    { "68", null, "WARNING", true, "DOCUMENTO SUSTENTO" },
                    { "69", null, "ERROR", true, "IDENTIFICACIÓN DEL RECEPTOR" },
                    { "70", null, "ERROR", true, "CLAVE DE ACCESO EN PROCESAMIENTO" },
                    { "80", null, "ERROR", true, "ERROR EN LA ESTRUCTURA DE CLAVE ACCESO" },
                    { "82", null, "ERROR", true, "ERROR EN LA FECHA DE INICIO DE TRANSPORTE" },
                    { "92", null, "ERROR", true, "ERROR AL VALIDAR MONTO DE DEVOLUCIÓN DEL IVA" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_ice_rate",
                columns: new[] { "code", "calculation_type", "is_active", "name", "percentage", "unit_value" },
                values: new object[,]
                {
                    { "3011", 1, true, "Cigarrillos rubios importados", 150.00m, null },
                    { "3021", 1, true, "Cigarrillos negros nacionales", 150.00m, null },
                    { "3041", 1, true, "Bebidas gaseosas con azúcar añadida", 10.00m, null },
                    { "3051", 1, true, "Bebidas energizantes", 10.00m, null },
                    { "3053", 2, true, "Bebidas gaseosas con alto contenido de azúcar", null, null },
                    { "3071", 1, true, "Perfumes y aguas de tocador", 20.00m, null },
                    { "3072", 1, true, "Videojuegos", 35.00m, null },
                    { "3073", 1, true, "Armas de fuego deportivas", 300.00m, null },
                    { "3081", 1, true, "Vehículos ≤3.5t (hasta USD 30k)", 5.00m, null },
                    { "3082", 1, true, "Vehículos ≤3.5t (USD 30k–40k)", 10.00m, null },
                    { "3083", 1, true, "Vehículos ≤3.5t (más de USD 40k)", 15.00m, null },
                    { "3091", 1, true, "Aviones / helicópteros de uso privado", 15.00m, null },
                    { "3101", 1, true, "Servicios de televisión pagada", 15.00m, null },
                    { "3111", 1, true, "Bebidas alcohólicas (incl. cerveza)", 75.00m, null }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_id_type",
                columns: new[] { "code", "digits", "name" },
                values: new object[,]
                {
                    { "04", (short)13, "Registro Único de Contribuyentes" },
                    { "05", (short)10, "Cédula de ciudadanía" },
                    { "06", null, "Pasaporte" },
                    { "07", null, "Consumidor Final" },
                    { "08", null, "Identificación del exterior" },
                    { "09", null, "Placa" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_irbpnr_rate",
                columns: new[] { "code", "calculation_type", "is_active", "name", "percentage", "unit_value" },
                values: new object[] { "5001", 2, true, "Impuesto Redimible a las Botellas Plásticas No Retornables", null, 0.02m });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_payment_method",
                columns: new[] { "code", "is_active", "name" },
                values: new object[,]
                {
                    { "01", true, "Sin utilización del sistema financiero" },
                    { "15", true, "Compensación de deudas" },
                    { "16", true, "Tarjeta de débito" },
                    { "17", true, "Dinero electrónico" },
                    { "18", true, "Tarjeta prepago" },
                    { "19", true, "Tarjeta de crédito" },
                    { "20", true, "Otros con utilización del sistema financiero" },
                    { "21", true, "Endoso de títulos" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_retention_code",
                columns: new[] { "id", "applies_to", "code", "is_active", "name", "percentage", "tax_type" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "SUPPLIER", "721", true, "Ret. IVA 10% – Bienes (tarifa vigente)", 10.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "SUPPLIER", "723", true, "Ret. IVA 20% – Servicios (tarifa vigente)", 20.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "SUPPLIER", "725", true, "Ret. IVA 30% – Presuntivo bienes", 30.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "SUPPLIER", "726", true, "Ret. IVA 70% – Presuntivo servicios", 70.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "SUPPLIER", "727", true, "Ret. IVA 100% – Liq. compra / honorarios", 100.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "SUPPLIER", "728", true, "Ret. IVA 15% – Constructoras", 15.00m, "IVA" },
                    { new Guid("20000000-0000-0000-0000-000000000001"), "SUPPLIER", "303", true, "Honorarios profesionales y demás servicios", 10.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "SUPPLIER", "304", true, "Servicios – predomina mano de obra", 2.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "SUPPLIER", "307", true, "Publicidad y comunicación", 1.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "SUPPLIER", "309", true, "Arrendamiento bienes inmuebles (persona natural)", 8.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000005"), "SUPPLIER", "310", true, "Seguros y reaseguros (10% de primas)", 1.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000006"), "SUPPLIER", "312", true, "Transf. bienes muebles de naturaleza corporal", 1.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000007"), "SUPPLIER", "320", true, "Servicios entre sociedades", 2.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000008"), "SUPPLIER", "325", true, "Compra bienes corporales muebles", 1.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000009"), "SUPPLIER", "327", true, "Actividades de construcción (contrato)", 1.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000010"), "SUPPLIER", "341", true, "Otras retenciones aplicables al 2%", 2.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000011"), "SUPPLIER", "342", true, "Otras retenciones aplicables al 1%", 1.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000012"), "SUPPLIER", "343", true, "Otras retenciones aplicables al 1.75%", 1.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000013"), "SUPPLIER", "344", true, "Otras retenciones aplicables al 2.75%", 2.75m, "RENTA" },
                    { new Guid("30000000-0000-0000-0000-000000000001"), "SUPPLIER", "4580", true, "ISD – Impuesto a la Salida de Divisas", 5.00m, "ISD" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_supplier_type",
                columns: new[] { "code", "is_active", "name" },
                values: new object[,]
                {
                    { "01", true, "Persona Natural" },
                    { "02", true, "Sociedad" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_tax_regime",
                columns: new[] { "code", "abbrev", "is_active", "name" },
                values: new object[,]
                {
                    { "01", "GENERAL", true, "Régimen General" },
                    { "02", "RIMPE_ME", true, "RIMPE – Régimen de Microempresas" },
                    { "03", "RIMPE_NP", true, "RIMPE – Negocio Popular" },
                    { "04", "ESP", true, "Contribuyente Especial" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_tax_support",
                columns: new[] { "code", "is_active", "name" },
                values: new object[,]
                {
                    { "01", true, "Crédito Tributario para declaración de IVA" },
                    { "02", true, "Costo o Gasto para declaración del IR" },
                    { "03", true, "Activo Fijo – Crédito Tributario IVA" },
                    { "04", true, "Activo Fijo – Costo o Gasto IR" },
                    { "05", true, "Liquidación Gastos de Viaje, Hospedaje y Alimentación" },
                    { "06", true, "Retención en la Fuente" },
                    { "07", true, "Distribución de Dividendos, Beneficios o Ganancias" },
                    { "08", true, "Impuesto a los Activos en el Exterior" },
                    { "09", true, "Retención del IVA 30%" },
                    { "10", true, "Retención del IVA 70%" },
                    { "11", true, "Retención del IVA 100%" },
                    { "12", true, "Exportación de Bienes" },
                    { "13", true, "No aplica" },
                    { "14", true, "Exportación de servicios con domicilio en el exterior" },
                    { "15", true, "Proveedor directo de exportador de bienes" },
                    { "16", true, "Provisiones de cuentas incobrables" },
                    { "17", true, "Nota de crédito deducible" },
                    { "18", true, "Importaciones" },
                    { "19", true, "Reembolso de gastos" },
                    { "20", true, "Notas de crédito por devoluciones" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_uom",
                columns: new[] { "code", "abbrev", "is_active", "name" },
                values: new object[,]
                {
                    { "01", "UB", true, "Unidad Biológica" },
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
                    { "12", "M3", true, "Metro cúbico" },
                    { "13", "ML", true, "Mililitro" },
                    { "14", "PAQ", true, "Paquete" },
                    { "15", "PAR", true, "Par" },
                    { "16", "QQ", true, "Quintal" },
                    { "17", "ROLLO", true, "Rollo" },
                    { "18", "TON", true, "Tonelada" },
                    { "19", "UN", true, "Unidad" },
                    { "20", "VEH", true, "Vehículo" },
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
                    { "10", true, "13% IVA", 13.00m, new DateOnly(2008, 1, 1), null }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_vat_rate",
                columns: new[] { "code", "name", "percentage", "valid_from", "valid_until" },
                values: new object[,]
                {
                    { "2", "12% IVA (histórico)", 12.00m, new DateOnly(2008, 1, 1), new DateOnly(2016, 5, 31) },
                    { "3", "14% IVA (histórico)", 14.00m, new DateOnly(2016, 6, 1), new DateOnly(2017, 5, 31) }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_vat_rate",
                columns: new[] { "code", "is_active", "name", "percentage", "valid_from", "valid_until" },
                values: new object[,]
                {
                    { "4", true, "15% IVA (tarifa general vigente)", 15.00m, new DateOnly(2024, 4, 1), null },
                    { "5", true, "5% IVA", 5.00m, new DateOnly(2024, 1, 1), null },
                    { "6", true, "No objeto de Impuesto", 0.00m, new DateOnly(2008, 1, 1), null },
                    { "7", true, "Exento de IVA", 0.00m, new DateOnly(2008, 1, 1), null },
                    { "8", true, "IVA diferenciado (tarifa variable por decreto — turismo)", 8.00m, new DateOnly(2008, 1, 1), null }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "doc_type_sri_map",
                columns: new[] { "doc_type_code", "sri_doc_type_code" },
                values: new object[,]
                {
                    { "FACVEN", "01" },
                    { "NCCDEV", "04" },
                    { "NCVDEV", "04" },
                    { "RETGAS", "07" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_id_type_usage",
                columns: new[] { "Id", "IdTypeCode", "IsActive", "UsageType" },
                values: new object[,]
                {
                    { new Guid("a0000001-0000-4000-9000-000000000001"), "04", true, "Customer" },
                    { new Guid("a0000001-0000-4000-9000-000000000002"), "04", true, "Supplier" },
                    { new Guid("a0000001-0000-4000-9000-000000000003"), "04", true, "Carrier" },
                    { new Guid("a0000001-0000-4000-9000-000000000004"), "05", true, "Customer" },
                    { new Guid("a0000001-0000-4000-9000-000000000005"), "05", true, "Employee" },
                    { new Guid("a0000001-0000-4000-9000-000000000006"), "05", true, "Carrier" },
                    { new Guid("a0000001-0000-4000-9000-000000000007"), "06", true, "Customer" },
                    { new Guid("a0000001-0000-4000-9000-000000000008"), "06", true, "Employee" },
                    { new Guid("a0000001-0000-4000-9000-000000000009"), "07", true, "Customer" },
                    { new Guid("a0000001-0000-4000-9000-00000000000a"), "08", true, "Customer" },
                    { new Guid("a0000001-0000-4000-9000-00000000000b"), "08", true, "Supplier" },
                    { new Guid("a0000001-0000-4000-9000-00000000000c"), "09", true, "Customer" },
                    { new Guid("a0000001-0000-4000-9000-00000000000d"), "09", true, "Carrier" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_profile_permissions_subscriber_key",
                table: "access_profile_permissions",
                columns: new[] { "tenant_id", "permission_key" });

            migrationBuilder.CreateIndex(
                name: "ux_access_profile_permissions_subscriber_profile_key",
                table: "access_profile_permissions",
                columns: new[] { "tenant_id", "profile_id", "permission_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_access_profiles_subscriber_name",
                table: "access_profiles",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_accounting_periods_company_year_period",
                table: "accounting_periods",
                columns: new[] { "company_id", "fiscal_year", "period_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_company_parent",
                table: "accounts",
                columns: new[] { "company_id", "parent_account_id" });

            migrationBuilder.CreateIndex(
                name: "uq_accounts_company_code",
                table: "accounts",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payable_installments_tenant_duedate",
                table: "accounts_payable_installments",
                columns: new[] { "tenant_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payable_installments_tenant_payable",
                table: "accounts_payable_installments",
                columns: new[] { "tenant_id", "accounts_payable_id" });

            migrationBuilder.CreateIndex(
                name: "uq_accounts_payable_installments_payable_number",
                table: "accounts_payable_installments",
                columns: new[] { "accounts_payable_id", "installment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_payables_branch_id",
                table: "accounts_payables",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_payables_company_id",
                table: "accounts_payables",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_payables_supplier_id",
                table: "accounts_payables",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payables_tenant_company_supplier_status",
                table: "accounts_payables",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_accounts_payables_tenant_company_origin",
                table: "accounts_payables",
                columns: new[] { "tenant_id", "company_id", "origin_type", "origin_id" },
                unique: true);

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
                name: "IX_attribute_definitions_group_id",
                table: "attribute_definitions",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_definitions_variant_axis",
                table: "attribute_definitions",
                columns: new[] { "tenant_id", "is_variant_axis" });

            migrationBuilder.CreateIndex(
                name: "uq_attribute_definitions_subscriber_group_code",
                table: "attribute_definitions",
                columns: new[] { "tenant_id", "group_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_attribute_groups_subscriber_code",
                table: "attribute_groups",
                columns: new[] { "tenant_id", "code" },
                unique: true);

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
                name: "ix_branches_tenant_id",
                table: "branches",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_branches_tenant_company_main",
                table: "branches",
                columns: new[] { "tenant_id", "company_id" },
                unique: true,
                filter: "is_main_branch = true");

            migrationBuilder.CreateIndex(
                name: "ix_brands_subscriber_code",
                table: "brands",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_closing_counts_cash_session_id",
                table: "cash_closing_counts",
                column: "cash_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_closing_counts_tenant_session",
                table: "cash_closing_counts",
                columns: new[] { "tenant_id", "cash_session_id" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_movements_cash_session_id",
                table: "cash_movements",
                column: "cash_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_movements_tenant_ref",
                table: "cash_movements",
                columns: new[] { "tenant_id", "reference_type", "reference_id" },
                filter: "reference_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_cash_movements_tenant_session",
                table: "cash_movements",
                columns: new[] { "tenant_id", "cash_session_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_movements_tenant_type",
                table: "cash_movements",
                columns: new[] { "tenant_id", "movement_type" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_branch_id",
                table: "cash_registers",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_company_id",
                table: "cash_registers",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_default_customer_id",
                table: "cash_registers",
                column: "default_customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_default_warehouse_id",
                table: "cash_registers",
                column: "default_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_registers_emission_point",
                table: "cash_registers",
                column: "emission_point_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_registers_tenant_branch",
                table: "cash_registers",
                columns: new[] { "tenant_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_registers_tenant_company",
                table: "cash_registers",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_cash_registers_tenant_branch_code",
                table: "cash_registers",
                columns: new[] { "tenant_id", "branch_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_sessions_branch_id",
                table: "cash_sessions",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_sessions_cash_register_id",
                table: "cash_sessions",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_sessions_company_id",
                table: "cash_sessions",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_sessions_emission_point_id",
                table: "cash_sessions",
                column: "emission_point_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_sessions_tenant_company",
                table: "cash_sessions",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_sessions_tenant_ep",
                table: "cash_sessions",
                columns: new[] { "tenant_id", "emission_point_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_sessions_tenant_register_status",
                table: "cash_sessions",
                columns: new[] { "tenant_id", "cash_register_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_sessions_tenant_status",
                table: "cash_sessions",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_sessions_tenant_user_status",
                table: "cash_sessions",
                columns: new[] { "tenant_id", "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_cash_sessions_open_per_register",
                table: "cash_sessions",
                columns: new[] { "tenant_id", "cash_register_id" },
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ux_cash_sessions_open_per_user",
                table: "cash_sessions",
                columns: new[] { "tenant_id", "user_id" },
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ix_communication_outbox_correlation",
                table: "communication_outbox",
                columns: new[] { "tenant_id", "company_id", "correlation_type", "correlation_id", "purpose", "recipient_email" });

            migrationBuilder.CreateIndex(
                name: "ix_communication_outbox_due",
                table: "communication_outbox",
                columns: new[] { "tenant_id", "company_id", "status", "scheduled_at_utc", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_communication_outbox_idempotency",
                table: "communication_outbox",
                columns: new[] { "tenant_id", "company_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_communication_outbox_attachments_outbox",
                table: "communication_outbox_attachments",
                column: "communication_outbox_id");

            migrationBuilder.CreateIndex(
                name: "ux_communication_templates_code_language",
                table: "communication_templates",
                columns: new[] { "tenant_id", "company_id", "channel", "code", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_country_code",
                table: "company",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "IX_company_tax_regime_code",
                table: "company",
                column: "tax_regime_code");

            migrationBuilder.CreateIndex(
                name: "ix_company_tenant_id",
                table: "company",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_tax_identification_number",
                table: "company",
                column: "tax_identification_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_financial_destination_audit_company_occurred_at",
                table: "company_financial_destination_audit",
                columns: new[] { "tenant_id", "company_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_company_financial_destination_audit_entity_occurred_at",
                table: "company_financial_destination_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_company_financial_destination_audit_user_occurred_at",
                table: "company_financial_destination_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_company_financial_destinations_accounting_account_id",
                table: "company_financial_destinations",
                column: "accounting_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_financial_destinations_cash_register_id",
                table: "company_financial_destinations",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_financial_destinations_company_id",
                table: "company_financial_destinations",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_financial_destinations_tenant_company",
                table: "company_financial_destinations",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_company_financial_destinations_bank_identity",
                table: "company_financial_destinations",
                columns: new[] { "tenant_id", "company_id", "bank_institution_code", "bank_account_identifier_normalized" },
                unique: true,
                filter: "\"destination_type_code\" = 1");

            migrationBuilder.CreateIndex(
                name: "uq_company_financial_destinations_cash_register",
                table: "company_financial_destinations",
                columns: new[] { "tenant_id", "company_id", "cash_register_id" },
                unique: true,
                filter: "\"destination_type_code\" = 2");

            migrationBuilder.CreateIndex(
                name: "uq_company_financial_destinations_tenant_company_code",
                table: "company_financial_destinations",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_special_tax_responsibilities_tenant",
                table: "company_special_tax_responsibilities",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_special_tax_responsibility",
                table: "company_special_tax_responsibilities",
                columns: new[] { "company_id", "sri_tax_category_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_user_branches_branch",
                table: "company_user_branches",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_user_branches_company",
                table: "company_user_branches",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ux_company_user_branches_membership_branch",
                table: "company_user_branches",
                columns: new[] { "company_user_membership_id", "branch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_company_user_memberships_company_identity_user",
                table: "company_user_memberships",
                columns: new[] { "company_id", "identity_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_user_preferences_company",
                table: "company_user_preferences",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_user_preferences_default_branch",
                table: "company_user_preferences",
                column: "default_branch_id");

            migrationBuilder.CreateIndex(
                name: "ux_company_user_preferences_membership",
                table: "company_user_preferences",
                column: "company_user_membership_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_config_feature_subscriber_feature_key",
                table: "config_feature",
                columns: new[] { "tenant_id", "feature", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_config_global_subscriber_key",
                table: "config_global",
                columns: new[] { "tenant_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_config_module_subscriber_module_key",
                table: "config_module",
                columns: new[] { "tenant_id", "module", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_config_change_log_tenant_company_changed_at",
                table: "configuration_change_log",
                columns: new[] { "tenant_id", "company_id", "changed_at_utc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_config_change_log_tenant_company_entity",
                table: "configuration_change_log",
                columns: new[] { "tenant_id", "company_id", "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_config_change_log_tenant_company_key",
                table: "configuration_change_log",
                columns: new[] { "tenant_id", "company_id", "key" });

            migrationBuilder.CreateIndex(
                name: "ix_config_change_log_tenant_company_scope",
                table: "configuration_change_log",
                columns: new[] { "tenant_id", "company_id", "scope", "scope_id" });

            migrationBuilder.CreateIndex(
                name: "uq_credit_installments_term_number",
                table: "credit_installments",
                columns: new[] { "credit_term_id", "installment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_credit_terms_tenant_company",
                table: "credit_terms",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_credit_terms_tenant_company_code",
                table: "credit_terms",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_current_stocks_tenant_company",
                table: "current_stocks",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_current_stocks_tenant_product_warehouse",
                table: "current_stocks",
                columns: new[] { "tenant_id", "product_id", "warehouse_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_doc_type_sri_map_sri_doc_type_code",
                schema: "global",
                table: "doc_type_sri_map",
                column: "sri_doc_type_code");

            migrationBuilder.CreateIndex(
                name: "IX_document_flow_policy_document_type_code",
                table: "document_flow_policy",
                column: "document_type_code");

            migrationBuilder.CreateIndex(
                name: "uq_document_flow_policy_company_doc_type",
                table: "document_flow_policy",
                columns: new[] { "tenant_id", "company_id", "document_type_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_docseq_company",
                table: "document_sequence",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_sequence_doc_type_code",
                table: "document_sequence",
                column: "doc_type_code");

            migrationBuilder.CreateIndex(
                name: "IX_document_sequence_emission_point_id",
                table: "document_sequence",
                column: "emission_point_id");

            migrationBuilder.CreateIndex(
                name: "uq_doc_seq",
                table: "document_sequence",
                columns: new[] { "tenant_id", "company_id", "emission_point_id", "doc_type_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_electronic_document_audit_company_occurred_at",
                table: "electronic_document_audit",
                columns: new[] { "tenant_id", "company_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_electronic_document_audit_entity_occurred_at",
                table: "electronic_document_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_electronic_document_audit_user_occurred_at",
                table: "electronic_document_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_electronic_document_sri_message_entity_occurred_at",
                table: "electronic_document_sri_message",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_electronic_document_sri_message_user_occurred_at",
                table: "electronic_document_sri_message",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "idx_electronic_document_company",
                table: "electronic_documents",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_electronic_document_access_key",
                table: "electronic_documents",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_electronic_document_source",
                table: "electronic_documents",
                columns: new[] { "tenant_id", "source_module", "source_entity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_emission_point_company_id",
                table: "emission_point",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_emission_point_tenant_id",
                table: "emission_point",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_emission_point_establishment_default",
                table: "emission_point",
                columns: new[] { "tenant_id", "company_id", "establishment_id" },
                unique: true,
                filter: "is_default = true");

            migrationBuilder.CreateIndex(
                name: "uq_ep_code",
                table: "emission_point",
                columns: new[] { "establishment_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_establishment_branch_id",
                table: "establishment",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_establishment_tenant_branch",
                table: "establishment",
                columns: new[] { "tenant_id", "branch_id" },
                filter: "branch_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_establishment_tenant_id",
                table: "establishment",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_estab_code",
                table: "establishment",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_establishment_tenant_company_main",
                table: "establishment",
                columns: new[] { "tenant_id", "company_id" },
                unique: true,
                filter: "is_main = true");

            migrationBuilder.CreateIndex(
                name: "IX_expense_category_nodes_accounting_account_id",
                table: "expense_category_nodes",
                column: "accounting_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_category_nodes_company_id",
                table: "expense_category_nodes",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_category_nodes_parent_id",
                table: "expense_category_nodes",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_category_nodes_tenant_company",
                table: "expense_category_nodes",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_category_nodes_tenant_company_active",
                table: "expense_category_nodes",
                columns: new[] { "tenant_id", "company_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_expense_category_nodes_parent_code",
                table: "expense_category_nodes",
                columns: new[] { "tenant_id", "company_id", "parent_id", "level", "code" },
                unique: true,
                filter: "\"parent_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_expense_category_nodes_parent_name",
                table: "expense_category_nodes",
                columns: new[] { "tenant_id", "company_id", "parent_id", "level", "name" },
                unique: true,
                filter: "\"parent_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_expense_category_nodes_root_code",
                table: "expense_category_nodes",
                columns: new[] { "tenant_id", "company_id", "level", "code" },
                unique: true,
                filter: "\"parent_id\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_expense_category_nodes_root_name",
                table: "expense_category_nodes",
                columns: new[] { "tenant_id", "company_id", "level", "name" },
                unique: true,
                filter: "\"parent_id\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_expense_documents_branch_id",
                table: "expense_documents",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_documents_company_id",
                table: "expense_documents",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_documents_payment_term_id",
                table: "expense_documents",
                column: "payment_term_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_documents_supplier_id",
                table: "expense_documents",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_documents_tenant_company",
                table: "expense_documents",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_documents_tenant_company_issue_date",
                table: "expense_documents",
                columns: new[] { "tenant_id", "company_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_documents_tenant_company_status",
                table: "expense_documents",
                columns: new[] { "tenant_id", "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_expense_documents_tenant_company_supplier_type_number",
                table: "expense_documents",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "document_type", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_lines_document",
                table: "expense_lines",
                column: "expense_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_lines_expense_subcategory_id",
                table: "expense_lines",
                column: "expense_subcategory_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_lines_snapshot_accounting_account_id",
                table: "expense_lines",
                column: "snapshot_accounting_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_lines_tenant",
                table: "expense_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_lines_tenant_subcategory",
                table: "expense_lines",
                columns: new[] { "tenant_id", "expense_subcategory_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_payment_schedules_tenant_document",
                table: "expense_payment_schedules",
                columns: new[] { "tenant_id", "expense_document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_payment_schedules_tenant_duedate",
                table: "expense_payment_schedules",
                columns: new[] { "tenant_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "uq_expense_payment_schedules_document_number",
                table: "expense_payment_schedules",
                columns: new[] { "expense_document_id", "installment_number" },
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
                unique: true,
                filter: "email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_identity_users_email_normalized",
                table: "identity_users",
                column: "email_normalized",
                unique: true,
                filter: "email_normalized IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_identity_users_username_normalized",
                table: "identity_users",
                column: "username_normalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_import_batch_files_batch",
                table: "import_batch_files",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_batch_issues_batch_severity",
                table: "import_batch_issues",
                columns: new[] { "import_batch_id", "severity" });

            migrationBuilder.CreateIndex(
                name: "ix_import_batch_issues_row",
                table: "import_batch_issues",
                column: "import_batch_row_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_batch_rows_batch_blocking",
                table: "import_batch_rows",
                columns: new[] { "import_batch_id", "has_blocking_issue" });

            migrationBuilder.CreateIndex(
                name: "ix_import_batch_rows_batch_row_number",
                table: "import_batch_rows",
                columns: new[] { "import_batch_id", "row_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_company",
                table: "import_batches",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_company_type_status",
                table: "import_batches",
                columns: new[] { "tenant_id", "company_id", "import_type", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_inventory_adjustment_reasons_tenant_code",
                table: "inventory_adjustment_reasons",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inv_lots_tenant_company_item",
                table: "inventory_lots",
                columns: new[] { "tenant_id", "company_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "uq_inv_lots_item_number",
                table: "inventory_lots",
                columns: new[] { "tenant_id", "company_id", "item_id", "lot_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inv_serials_tenant_company_item",
                table: "inventory_serials",
                columns: new[] { "tenant_id", "company_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "uq_inv_serials_item_serial",
                table: "inventory_serials",
                columns: new[] { "tenant_id", "company_id", "item_id", "serial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_issued_withholding_audit_entity_occurred_at",
                table: "issued_withholding_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_issued_withholding_audit_invoice_occurred_at",
                table: "issued_withholding_audit",
                columns: new[] { "tenant_id", "purchase_invoice_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_issued_withholding_audit_user_occurred_at",
                table: "issued_withholding_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_issued_wh_details_tenant",
                table: "issued_withholding_details",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_issued_wh_details_withholding",
                table: "issued_withholding_details",
                column: "withholding_id");

            migrationBuilder.CreateIndex(
                name: "IX_issued_withholdings_emission_point_id",
                table: "issued_withholdings",
                column: "emission_point_id");

            migrationBuilder.CreateIndex(
                name: "IX_issued_withholdings_supplier_id",
                table: "issued_withholdings",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_issued_withholdings_tenant_company",
                table: "issued_withholdings",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_issued_withholdings_number",
                table: "issued_withholdings",
                columns: new[] { "tenant_id", "company_id", "withholding_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_issued_withholdings_purchase",
                table: "issued_withholdings",
                column: "purchase_invoice_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_audit_entity_occurred_at",
                table: "item_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_item_audit_user_occurred_at",
                table: "item_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_item_category_nodes_ParentId",
                table: "item_category_nodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_item_category_nodes_TenantId",
                table: "item_category_nodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_item_category_nodes_TenantId_Code",
                table: "item_category_nodes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_category_nodes_TenantId_IsActive",
                table: "item_category_nodes",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_item_category_nodes_TenantId_ParentId",
                table: "item_category_nodes",
                columns: new[] { "TenantId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_item_category_nodes_TenantId_Path",
                table: "item_category_nodes",
                columns: new[] { "TenantId", "Path" });

            migrationBuilder.CreateIndex(
                name: "ix_item_images_item",
                table: "item_images",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_packaging_levels_barcode",
                table: "item_packaging_levels",
                column: "barcode",
                filter: "barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_item_packaging_level",
                table: "item_packaging_levels",
                columns: new[] { "item_id", "level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_item_packaging_uom",
                table: "item_packaging_levels",
                columns: new[] { "item_id", "uom_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_special_tax_configurations_tenant",
                table: "item_special_tax_configurations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_item_special_tax_configuration",
                table: "item_special_tax_configurations",
                columns: new[] { "item_id", "sri_tax_category_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_item_substitute",
                table: "item_substitutes",
                columns: new[] { "item_id", "substitute_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_supplier_codes_item_active",
                table: "item_supplier_codes",
                columns: new[] { "item_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_item_supplier_codes_packaging_level",
                table: "item_supplier_codes",
                column: "packaging_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_supplier_codes_supplier_id",
                table: "item_supplier_codes",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "uq_item_supplier_codes_primary",
                table: "item_supplier_codes",
                columns: new[] { "item_id", "is_primary" },
                unique: true,
                filter: "is_primary = true");

            migrationBuilder.CreateIndex(
                name: "uq_item_supplier_codes_tenant_supplier_code",
                table: "item_supplier_codes",
                columns: new[] { "tenant_id", "supplier_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_types_tenant_active",
                table: "item_types",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_item_types_tenant_code",
                table: "item_types",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_item_unit_conversion",
                table: "item_unit_conversions",
                columns: new[] { "item_id", "from_uom_code", "to_uom_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_item_variant_attribute",
                table: "item_variant_attributes",
                columns: new[] { "variant_id", "attribute_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_variant_barcodes_barcode_type",
                table: "item_variant_barcodes",
                column: "barcode_type");

            migrationBuilder.CreateIndex(
                name: "IX_item_variant_barcodes_variant_id",
                table: "item_variant_barcodes",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "uq_item_variant_barcode_tenant_code",
                table: "item_variant_barcodes",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_item_variant_barcodes_primary",
                table: "item_variant_barcodes",
                column: "item_id",
                unique: true,
                filter: "is_primary = true");

            migrationBuilder.CreateIndex(
                name: "ix_item_variants_item",
                table: "item_variants",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "uq_item_variants_tenant_sku",
                table: "item_variants",
                columns: new[] { "tenant_id", "sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_items_brand_id",
                table: "items",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_category_node_id",
                table: "items",
                column: "category_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_item_type_id",
                table: "items",
                column: "item_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_items_subscriber",
                table: "items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_items_subscriber_category",
                table: "items",
                columns: new[] { "tenant_id", "category_node_id" });

            migrationBuilder.CreateIndex(
                name: "ix_items_subscriber_type",
                table: "items",
                columns: new[] { "tenant_id", "item_type_id" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_accounting_period_id",
                table: "journal_entries",
                column: "accounting_period_id");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_reverse_journal_entry_id",
                table: "journal_entries",
                column: "reverse_journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "uq_journal_entries_company_fiscal_year_entry_number",
                table: "journal_entries",
                columns: new[] { "company_id", "fiscal_year", "entry_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_journal_entries_company_source_event_fact",
                table: "journal_entries",
                columns: new[] { "company_id", "source_module", "source_event_id", "source_event_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_journal_entries_original_journal_entry_id",
                table: "journal_entries",
                column: "original_journal_entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_account_id",
                table: "journal_entry_lines",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entry_lines_journal_entry",
                table: "journal_entry_lines",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entry_lines_tenant",
                table: "journal_entry_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_journal_entry_sequences_company_fiscal_year",
                table: "journal_entry_sequences",
                columns: new[] { "tenant_id", "company_id", "fiscal_year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bpc_location",
                table: "master_bp_contacts",
                column: "location_id",
                filter: "location_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_bpc_subscriber_bp_active",
                table: "master_bp_contacts",
                columns: new[] { "tenant_id", "business_partner_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_bpc_subscriber_bp_role",
                table: "master_bp_contacts",
                columns: new[] { "tenant_id", "business_partner_id", "contact_role" });

            migrationBuilder.CreateIndex(
                name: "IX_master_bp_contacts_business_partner_id",
                table: "master_bp_contacts",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "uq_bpc_primary",
                table: "master_bp_contacts",
                columns: new[] { "tenant_id", "business_partner_id" },
                unique: true,
                filter: "is_primary = true AND is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_bpcrc_role",
                table: "master_bp_customer_configs",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpl_subscriber_bp_active",
                table: "master_bp_locations",
                columns: new[] { "tenant_id", "business_partner_id", "is_active" });

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
                columns: new[] { "tenant_id", "business_partner_id" },
                unique: true,
                filter: "is_primary = true AND is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_bpr_subscriber_bp",
                table: "master_bp_roles",
                columns: new[] { "tenant_id", "business_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bpr_subscriber_type",
                table: "master_bp_roles",
                columns: new[] { "tenant_id", "role_type" });

            migrationBuilder.CreateIndex(
                name: "ix_bpr_subscriber_type_active",
                table: "master_bp_roles",
                columns: new[] { "tenant_id", "role_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_master_bp_roles_business_partner_id",
                table: "master_bp_roles",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "uq_bpr_bp_role",
                table: "master_bp_roles",
                columns: new[] { "tenant_id", "business_partner_id", "role_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_master_business_partners_legal_entity_type_code",
                table: "master_business_partners",
                column: "legal_entity_type_code");

            migrationBuilder.CreateIndex(
                name: "ix_mbp_subscriber",
                table: "master_business_partners",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_mbp_subscriber_active",
                table: "master_business_partners",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_cbts_blocked",
                table: "master_company_bp_trading_settings",
                columns: new[] { "tenant_id", "company_id" },
                filter: "is_blocked = true");

            migrationBuilder.CreateIndex(
                name: "IX_master_company_bp_trading_settings_business_partner_id",
                table: "master_company_bp_trading_settings",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "uq_cbts_company_bp",
                table: "master_company_bp_trading_settings",
                columns: new[] { "tenant_id", "company_id", "business_partner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_customer_categories_tenant_company",
                table: "master_customer_categories",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_customer_categories_tenant_company_code",
                table: "master_customer_categories",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_customer_classifications_tenant_company",
                table: "master_customer_classifications",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_customer_classifications_tenant_company_code",
                table: "master_customer_classifications",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_customer_credit_ratings_tenant_company",
                table: "master_customer_credit_ratings",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_customer_credit_ratings_tenant_company_code",
                table: "master_customer_credit_ratings",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_customer_invoice_formats_tenant_company",
                table: "master_customer_invoice_formats",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_customer_invoice_formats_tenant_company_code",
                table: "master_customer_invoice_formats",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_customer_loyalty_tiers_tenant_company",
                table: "master_customer_loyalty_tiers",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_customer_loyalty_tiers_tenant_company_code",
                table: "master_customer_loyalty_tiers",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_customer_segments_tenant_company",
                table: "master_customer_segments",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_customer_segments_tenant_company_code",
                table: "master_customer_segments",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_terms_tenant",
                table: "master_payment_terms",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_payment_terms_tenant_code",
                table: "master_payment_terms",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_supplier_categories_tenant_company",
                table: "master_supplier_categories",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_supplier_categories_tenant_company_code",
                table: "master_supplier_categories",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_supplier_good_types_tenant_company",
                table: "master_supplier_good_types",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_supplier_good_types_tenant_company_code",
                table: "master_supplier_good_types",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_supplier_ratings_tenant_company",
                table: "master_supplier_ratings",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_supplier_ratings_tenant_company_code",
                table: "master_supplier_ratings",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_supplier_risks_tenant_company",
                table: "master_supplier_risks",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_supplier_risks_tenant_company_code",
                table: "master_supplier_risks",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_supplier_segments_tenant_company",
                table: "master_supplier_segments",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_supplier_segments_tenant_company_code",
                table: "master_supplier_segments",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_supplier_types_tenant_company",
                table: "master_supplier_types",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_supplier_types_tenant_company_code",
                table: "master_supplier_types",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_files_owner_role_primary",
                table: "media_files",
                columns: new[] { "tenant_id", "company_id", "owner_type", "owner_id", "role", "is_primary", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_media_files_tenant_company",
                table: "media_files",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_org_settings_scope_lookup",
                table: "org_settings",
                columns: new[] { "tenant_id", "company_id", "scope", "scope_id" });

            migrationBuilder.CreateIndex(
                name: "ix_org_settings_tenant_company",
                table: "org_settings",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_org_settings_scope_key",
                table: "org_settings",
                columns: new[] { "tenant_id", "company_id", "scope", "scope_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_EventName",
                table: "OutboxMessages",
                columns: new[] { "EventName", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Tenant",
                table: "OutboxMessages",
                columns: new[] { "TenantId", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_user",
                table: "password_reset_tokens",
                columns: new[] { "user_id", "user_kind", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_application_lines_payable",
                table: "payment_application_lines",
                column: "payable_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_application_lines_payment",
                table: "payment_application_lines",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_application_lines_receivable",
                table: "payment_application_lines",
                column: "receivable_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_methods_tenant_active",
                table: "payment_methods",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_payment_methods_tenant_code",
                table: "payment_methods",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_financial_destination_id",
                table: "payments",
                column: "financial_destination_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_partner_id",
                table: "payments",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_payment_method_id",
                table: "payments",
                column: "payment_method_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_company",
                table: "payments",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_company_status",
                table: "payments",
                columns: new[] { "tenant_id", "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_partner",
                table: "payments",
                columns: new[] { "tenant_id", "partner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_posting_rule_lines_posting_rule",
                table: "posting_rule_lines",
                column: "posting_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_posting_rule_lines_tenant",
                table: "posting_rule_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_posting_rules_company_source_fact",
                table: "posting_rules",
                columns: new[] { "company_id", "source_module", "fact_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_price_list_audit_entity_occurred_at",
                table: "price_list_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_price_list_audit_user_occurred_at",
                table: "price_list_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_price_list_item_audit_entity_occurred_at",
                table: "price_list_item_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_price_list_item_audit_item_occurred_at",
                table: "price_list_item_audit",
                columns: new[] { "tenant_id", "item_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_price_list_item_audit_price_list_occurred_at",
                table: "price_list_item_audit",
                columns: new[] { "tenant_id", "price_list_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_price_list_item_audit_user_occurred_at",
                table: "price_list_item_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_price_list_items_item",
                table: "price_list_items",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "uq_price_list_items_list_item",
                table: "price_list_items",
                columns: new[] { "price_list_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_price_lists_tenant_company",
                table: "price_lists",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_price_lists_tenant_company_code",
                table: "price_lists",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_price_lists_tenant_company_default",
                table: "price_lists",
                columns: new[] { "tenant_id", "company_id", "is_default" },
                unique: true,
                filter: "is_default = true");

            migrationBuilder.CreateIndex(
                name: "ix_pricing_rule_audit_entity_occurred_at",
                table: "pricing_rule_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_pricing_rule_audit_item_occurred_at",
                table: "pricing_rule_audit",
                columns: new[] { "tenant_id", "item_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_pricing_rule_audit_price_list_occurred_at",
                table: "pricing_rule_audit",
                columns: new[] { "tenant_id", "price_list_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_pricing_rule_audit_user_occurred_at",
                table: "pricing_rule_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_pricing_rules_item",
                table: "pricing_rules",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "uq_pricing_rules_list_item",
                table: "pricing_rules",
                columns: new[] { "price_list_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_communications_purchase",
                table: "purchase_communications",
                column: "purchase_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_note_details_purchase_credit_note_id",
                table: "purchase_credit_note_details",
                column: "purchase_credit_note_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_note_details_tenant_purchase_credit_note",
                table: "purchase_credit_note_details",
                columns: new[] { "tenant_id", "purchase_credit_note_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_note_tax_summaries_branch_id",
                table: "purchase_credit_note_tax_summaries",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_note_tax_summaries_company_id",
                table: "purchase_credit_note_tax_summaries",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_note_tax_summaries_purchase_credit_note_id",
                table: "purchase_credit_note_tax_summaries",
                column: "purchase_credit_note_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_note_tax_summaries_purchase_invoice_id",
                table: "purchase_credit_note_tax_summaries",
                column: "purchase_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_note_tax_summaries_source_purchase_invoice_~",
                table: "purchase_credit_note_tax_summaries",
                column: "source_purchase_invoice_tax_summary_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_note_tax_summaries_tenant_company_branch",
                table: "purchase_credit_note_tax_summaries",
                columns: new[] { "tenant_id", "company_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_note_tax_summaries_tenant_credit_note",
                table: "purchase_credit_note_tax_summaries",
                columns: new[] { "tenant_id", "purchase_credit_note_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_note_tax_summaries_tenant_invoice",
                table: "purchase_credit_note_tax_summaries",
                columns: new[] { "tenant_id", "purchase_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_note_tax_summaries_tenant_source_summary",
                table: "purchase_credit_note_tax_summaries",
                columns: new[] { "tenant_id", "source_purchase_invoice_tax_summary_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_note_tax_summary_lines_summary",
                table: "purchase_credit_note_tax_summary_lines",
                column: "purchase_credit_note_tax_summary_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_note_tax_summary_lines_tenant",
                table: "purchase_credit_note_tax_summary_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_notes_branch_id",
                table: "purchase_credit_notes",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_notes_company_id",
                table: "purchase_credit_notes",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_notes_linked_purchase_return_id",
                table: "purchase_credit_notes",
                column: "linked_purchase_return_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_notes_purchase_invoice_id",
                table: "purchase_credit_notes",
                column: "purchase_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_notes_reception_document_id",
                table: "purchase_credit_notes",
                column: "reception_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_notes_supplier_id",
                table: "purchase_credit_notes",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_notes_tenant_company_branch",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "company_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_notes_tenant_purchase_invoice",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "purchase_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_notes_tenant_status",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_access_key",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "\"access_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_authorize_client_request_id",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "authorize_client_request_id" },
                unique: true,
                filter: "\"authorize_client_request_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_cancel_client_request_id",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "cancel_client_request_id" },
                unique: true,
                filter: "\"cancel_client_request_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_company_supplier_number",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "credit_note_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_create_client_request_id",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "create_client_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_linked_purchase_return_id",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "linked_purchase_return_id" },
                unique: true,
                filter: "\"linked_purchase_return_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_reception_document_id",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "reception_document_id" },
                unique: true,
                filter: "\"reception_document_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_audit_entity_occurred_at",
                table: "purchase_invoice_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_audit_supplier_occurred_at",
                table: "purchase_invoice_audit",
                columns: new[] { "tenant_id", "supplier_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_audit_user_occurred_at",
                table: "purchase_invoice_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_detail_taxes_detail",
                table: "purchase_invoice_detail_taxes",
                column: "purchase_invoice_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_detail_taxes_tenant",
                table: "purchase_invoice_detail_taxes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_details_invoice",
                table: "purchase_invoice_details",
                column: "purchase_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_details_item_id",
                table: "purchase_invoice_details",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_details_po_detail",
                table: "purchase_invoice_details",
                column: "purchase_order_detail_id",
                filter: "purchase_order_detail_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_details_reception_line",
                table: "purchase_invoice_details",
                column: "purchase_reception_line_id",
                filter: "purchase_reception_line_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_details_tenant",
                table: "purchase_invoice_details",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_details_tenant_item",
                table: "purchase_invoice_details",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_details_warehouse_id",
                table: "purchase_invoice_details",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_tax_summaries_branch_id",
                table: "purchase_invoice_tax_summaries",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_tax_summaries_company_id",
                table: "purchase_invoice_tax_summaries",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_tax_summaries_purchase_invoice_id",
                table: "purchase_invoice_tax_summaries",
                column: "purchase_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_tax_summaries_tenant_company_branch",
                table: "purchase_invoice_tax_summaries",
                columns: new[] { "tenant_id", "company_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_tax_summaries_tenant_invoice",
                table: "purchase_invoice_tax_summaries",
                columns: new[] { "tenant_id", "purchase_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_tax_summaries_tenant_invoice_vat_ice",
                table: "purchase_invoice_tax_summaries",
                columns: new[] { "tenant_id", "purchase_invoice_id", "vat_code", "ice_code" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_branch_id",
                table: "purchase_invoices",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_company_id",
                table: "purchase_invoices",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_global_warehouse_id",
                table: "purchase_invoices",
                column: "global_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_payment_term_id",
                table: "purchase_invoices",
                column: "payment_term_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoices_purchase_order",
                table: "purchase_invoices",
                column: "purchase_order_id",
                filter: "purchase_order_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_supplier_id",
                table: "purchase_invoices",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoices_tenant_company",
                table: "purchase_invoices",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoices_tenant_issue_date",
                table: "purchase_invoices",
                columns: new[] { "tenant_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoices_tenant_status",
                table: "purchase_invoices",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_purchase_invoices_tenant_access_key",
                table: "purchase_invoices",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_invoices_tenant_company_supplier_number",
                table: "purchase_invoices",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_line_pvp_audit_invoice_occurred_at",
                table: "purchase_line_pvp_audit",
                columns: new[] { "tenant_id", "purchase_invoice_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_line_pvp_audit_item_occurred_at",
                table: "purchase_line_pvp_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_line_pvp_audit_user_occurred_at",
                table: "purchase_line_pvp_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_payment_schedules_tenant_duedate",
                table: "purchase_payment_schedules",
                columns: new[] { "tenant_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_payment_schedules_tenant_invoice",
                table: "purchase_payment_schedules",
                columns: new[] { "tenant_id", "purchase_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "uq_purchase_payment_schedules_invoice_number",
                table: "purchase_payment_schedules",
                columns: new[] { "purchase_invoice_id", "installment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_reception_documents_branch_id",
                table: "purchase_reception_documents",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_reception_documents_company_id",
                table: "purchase_reception_documents",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_reception_documents_purchase_id",
                table: "purchase_reception_documents",
                column: "purchase_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_reception_documents_supplier_id",
                table: "purchase_reception_documents",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_documents_tenant_company",
                table: "purchase_reception_documents",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_documents_tenant_processing_status",
                table: "purchase_reception_documents",
                columns: new[] { "tenant_id", "processing_status" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_documents_tenant_status",
                table: "purchase_reception_documents",
                columns: new[] { "tenant_id", "document_status" });

            migrationBuilder.CreateIndex(
                name: "uq_purchase_reception_documents_tenant_access_key",
                table: "purchase_reception_documents",
                columns: new[] { "tenant_id", "access_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_line_additional_fields_line",
                table: "purchase_reception_line_additional_fields",
                column: "purchase_reception_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_line_additional_fields_tenant",
                table: "purchase_reception_line_additional_fields",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_line_taxes_line",
                table: "purchase_reception_line_taxes",
                column: "purchase_reception_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_line_taxes_tenant",
                table: "purchase_reception_line_taxes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_lines_document",
                table: "purchase_reception_lines",
                column: "purchase_reception_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_lines_item",
                table: "purchase_reception_lines",
                column: "item_id",
                filter: "item_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_audit_entity_occurred_at",
                table: "purchase_return_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_audit_purchase_invoice_occurred_at",
                table: "purchase_return_audit",
                columns: new[] { "tenant_id", "purchase_invoice_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_audit_supplier_occurred_at",
                table: "purchase_return_audit",
                columns: new[] { "tenant_id", "supplier_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_audit_user_occurred_at",
                table: "purchase_return_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_detail_taxes_detail",
                table: "purchase_return_detail_taxes",
                column: "purchase_return_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_detail_taxes_tenant",
                table: "purchase_return_detail_taxes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_details_original_invoice_detail_id",
                table: "purchase_return_details",
                column: "original_invoice_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_details_tenant_original_line",
                table: "purchase_return_details",
                columns: new[] { "tenant_id", "original_invoice_detail_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_details_tenant_purchase_return",
                table: "purchase_return_details",
                columns: new[] { "tenant_id", "purchase_return_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_details_warehouse_id",
                table: "purchase_return_details",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_return_details_return_original_line",
                table: "purchase_return_details",
                columns: new[] { "purchase_return_id", "original_invoice_detail_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_purchase_return_sequence_tenant_company",
                table: "purchase_return_sequence",
                columns: new[] { "tenant_id", "company_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_branch_id",
                table: "purchase_returns",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_company_id",
                table: "purchase_returns",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_purchase_invoice_id",
                table: "purchase_returns",
                column: "purchase_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_supplier_credit_note_document_id",
                table: "purchase_returns",
                column: "supplier_credit_note_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_supplier_id",
                table: "purchase_returns",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_returns_tenant_company_branch",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "company_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_returns_tenant_purchase_invoice",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "purchase_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_returns_tenant_status",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_purchase_returns_tenant_authorize_client_request_id",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "authorize_client_request_id" },
                unique: true,
                filter: "\"authorize_client_request_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_returns_tenant_cancel_client_request_id",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "cancel_client_request_id" },
                unique: true,
                filter: "\"cancel_client_request_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_returns_tenant_company_return_number",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "company_id", "return_number" },
                unique: true,
                filter: "\"return_number\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_returns_tenant_create_client_request_id",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "create_client_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_purchase_returns_tenant_link_credit_note_client_request_id",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "link_credit_note_client_request_id" },
                unique: true,
                filter: "\"link_credit_note_client_request_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_returns_tenant_supplier_credit_note_document_id",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "supplier_credit_note_document_id" },
                unique: true,
                filter: "\"supplier_credit_note_document_id\" IS NOT NULL");

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
                columns: new[] { "user_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "idx_ride_pdf_document_company",
                table: "ride_pdf_document",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_ride_pdf_document_fingerprint",
                table: "ride_pdf_document",
                columns: new[] { "tenant_id", "electronic_document_id", "source_xml_hash", "template_version", "branding_version", "renderer_version", "ride_specification_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_detail_taxes_detail",
                table: "sales_invoice_detail_taxes",
                column: "sales_invoice_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_detail_taxes_tenant",
                table: "sales_invoice_detail_taxes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_details_invoice_id",
                table: "sales_invoice_details",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_details_tenant_invoice",
                table: "sales_invoice_details",
                columns: new[] { "tenant_id", "invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_details_warehouse_id",
                table: "sales_invoice_details",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_payments_method",
                table: "sales_invoice_payments",
                column: "payment_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_payments_sales_invoice_id",
                table: "sales_invoice_payments",
                column: "sales_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_payments_tenant_invoice",
                table: "sales_invoice_payments",
                columns: new[] { "tenant_id", "sales_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_branch_id",
                table: "sales_invoices",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_cash_session",
                table: "sales_invoices",
                column: "cash_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_company_id",
                table: "sales_invoices",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_customer_id",
                table: "sales_invoices",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_emission_point_id",
                table: "sales_invoices",
                column: "emission_point_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_tenant_company",
                table: "sales_invoices",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_tenant_customer",
                table: "sales_invoices",
                columns: new[] { "tenant_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_tenant_issue_date",
                table: "sales_invoices",
                columns: new[] { "tenant_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_tenant_status",
                table: "sales_invoices",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_sales_invoices_tenant_company_number",
                table: "sales_invoices",
                columns: new[] { "tenant_id", "company_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_receivable_installments_due_date",
                table: "sales_receivable_installments",
                columns: new[] { "tenant_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "uq_sales_receivable_installments_number",
                table: "sales_receivable_installments",
                columns: new[] { "receivable_id", "installment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_receivables_customer_id",
                table: "sales_receivables",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_receivables_tenant_company",
                table: "sales_receivables",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_receivables_tenant_customer",
                table: "sales_receivables",
                columns: new[] { "tenant_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_receivables_tenant_status",
                table: "sales_receivables",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_sales_receivables_invoice",
                table: "sales_receivables",
                column: "invoice_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_audit_customer_occurred_at",
                table: "sales_return_audit",
                columns: new[] { "tenant_id", "customer_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_audit_entity_occurred_at",
                table: "sales_return_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_audit_sales_invoice_occurred_at",
                table: "sales_return_audit",
                columns: new[] { "tenant_id", "sales_invoice_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_audit_user_occurred_at",
                table: "sales_return_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_detail_taxes_detail",
                table: "sales_return_detail_taxes",
                column: "sales_return_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_detail_taxes_tenant",
                table: "sales_return_detail_taxes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_details_original_invoice_detail_id",
                table: "sales_return_details",
                column: "original_invoice_detail_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_details_return_id",
                table: "sales_return_details",
                column: "return_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_details_tenant_original_line",
                table: "sales_return_details",
                columns: new[] { "tenant_id", "original_invoice_detail_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_details_tenant_return",
                table: "sales_return_details",
                columns: new[] { "tenant_id", "return_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_details_warehouse_id",
                table: "sales_return_details",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_refund_allocations_return_id",
                table: "sales_return_refund_allocations",
                column: "return_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_refund_allocations_tenant_return",
                table: "sales_return_refund_allocations",
                columns: new[] { "tenant_id", "return_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_company_id",
                table: "sales_returns",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_customer_id",
                table: "sales_returns",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_sales_invoice_id",
                table: "sales_returns",
                column: "sales_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_tenant_company",
                table: "sales_returns",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_tenant_customer",
                table: "sales_returns",
                columns: new[] { "tenant_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_tenant_sales_invoice",
                table: "sales_returns",
                columns: new[] { "tenant_id", "sales_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_tenant_status",
                table: "sales_returns",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_sales_returns_tenant_company_number",
                table: "sales_returns",
                columns: new[] { "tenant_id", "company_id", "return_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_security_admin_scopes_subject",
                table: "security_admin_scope_assignments",
                columns: new[] { "tenant_id", "subject_type", "subject_key", "scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_sri_id_type_usage",
                schema: "global",
                table: "sri_id_type_usage",
                columns: new[] { "IdTypeCode", "UsageType" },
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
                name: "ix_stock_adjustment_lines_adjustment",
                table: "stock_adjustment_lines",
                column: "stock_adjustment_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_lines_item",
                table: "stock_adjustment_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_reason",
                table: "stock_adjustments",
                column: "reason_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_status",
                table: "stock_adjustments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_tenant_company",
                table: "stock_adjustments",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_warehouse",
                table: "stock_adjustments",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "uq_stock_adjustments_tenant_number",
                table: "stock_adjustments",
                columns: new[] { "tenant_id", "adjustment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_company_branch",
                table: "stock_movements",
                columns: new[] { "company_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_company_created_by_created_at",
                table: "stock_movements",
                columns: new[] { "company_id", "created_by", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_company_effective_date",
                table: "stock_movements",
                columns: new[] { "company_id", "effective_date" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_source_doc",
                table: "stock_movements",
                columns: new[] { "source_doc_id", "source_doc_type" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_tenant_company",
                table: "stock_movements",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_tenant_product_warehouse",
                table: "stock_movements",
                columns: new[] { "tenant_id", "product_id", "warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "uq_stock_movements_company_product_warehouse_sequence",
                table: "stock_movements",
                columns: new[] { "company_id", "product_id", "warehouse_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfer_lines_transfer",
                table: "stock_transfer_lines",
                column: "stock_transfer_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_operation_branch_id",
                table: "stock_transfers",
                column: "operation_branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_source_warehouse_id",
                table: "stock_transfers",
                column: "source_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_target_warehouse_id",
                table: "stock_transfers",
                column: "target_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfers_tenant_company",
                table: "stock_transfers",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_stock_transfers_tenant_company_number",
                table: "stock_transfers",
                columns: new[] { "tenant_id", "company_id", "transfer_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_audit_entity_occurred_at",
                table: "supplier_credit_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_audit_source_return_occurred_at",
                table: "supplier_credit_audit",
                columns: new[] { "tenant_id", "source_purchase_return_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_audit_supplier_occurred_at",
                table: "supplier_credit_audit",
                columns: new[] { "tenant_id", "supplier_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_audit_user_occurred_at",
                table: "supplier_credit_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_movements_supplier_credit_id",
                table: "supplier_credit_movements",
                column: "supplier_credit_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_movements_target_purchase_payable_id",
                table: "supplier_credit_movements",
                column: "target_purchase_payable_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_movements_tenant_supplier_credit",
                table: "supplier_credit_movements",
                columns: new[] { "tenant_id", "supplier_credit_id" });

            migrationBuilder.CreateIndex(
                name: "uq_supplier_credit_movements_reversal_of_movement",
                table: "supplier_credit_movements",
                column: "reversal_of_movement_id",
                unique: true,
                filter: "\"reversal_of_movement_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_supplier_credit_movements_tenant_client_request_id",
                table: "supplier_credit_movements",
                columns: new[] { "tenant_id", "client_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_accounting_account_id",
                table: "supplier_credit_refund_transactions",
                column: "accounting_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_cash_movement_id",
                table: "supplier_credit_refund_transactions",
                column: "cash_movement_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_cash_session_id",
                table: "supplier_credit_refund_transactions",
                column: "cash_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_company_id",
                table: "supplier_credit_refund_transactions",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_financial_destination_id",
                table: "supplier_credit_refund_transactions",
                column: "financial_destination_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_original_transaction_id",
                table: "supplier_credit_refund_transactions",
                column: "original_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_supplier_credit_id",
                table: "supplier_credit_refund_transactions",
                column: "supplier_credit_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_supplier_credit_movemen~",
                table: "supplier_credit_refund_transactions",
                column: "supplier_credit_movement_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_refund_transactions_tenant_company_account",
                table: "supplier_credit_refund_transactions",
                columns: new[] { "tenant_id", "company_id", "accounting_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_refund_transactions_tenant_company_credit",
                table: "supplier_credit_refund_transactions",
                columns: new[] { "tenant_id", "company_id", "supplier_credit_id" });

            migrationBuilder.CreateIndex(
                name: "uq_supplier_credit_refund_transactions_movement",
                table: "supplier_credit_refund_transactions",
                columns: new[] { "tenant_id", "company_id", "supplier_credit_movement_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_supplier_credit_refund_transactions_original",
                table: "supplier_credit_refund_transactions",
                columns: new[] { "tenant_id", "company_id", "original_transaction_id" },
                unique: true,
                filter: "\"transaction_type_code\" = 2");

            migrationBuilder.CreateIndex(
                name: "uq_supplier_credit_refund_transactions_tenant_client_request_id",
                table: "supplier_credit_refund_transactions",
                columns: new[] { "tenant_id", "client_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_branch_id",
                table: "supplier_credits",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_company_id",
                table: "supplier_credits",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_source_purchase_return_id",
                table: "supplier_credits",
                column: "source_purchase_return_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_supplier_id",
                table: "supplier_credits",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credits_tenant_company",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credits_tenant_supplier",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "uq_supplier_credits_tenant_source_purchase_return",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "source_purchase_return_id" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "ux_tenant_custom_menus_tenant",
                table: "tenant_custom_menus",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                table: "tenants",
                column: "slug",
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
                name: "ix_user_activity_subscriber_entity_created_at",
                table: "user_activity",
                columns: new[] { "tenant_id", "entity_type", "entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_activity_subscriber_module_created_at",
                table: "user_activity",
                columns: new[] { "tenant_id", "module", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_activity_subscriber_user_created_at",
                table: "user_activity",
                columns: new[] { "tenant_id", "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_branch_id",
                table: "user_sessions",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_company",
                table: "user_sessions",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_identity_user_tenant_status",
                table: "user_sessions",
                columns: new[] { "identity_user_id", "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_refresh_token_id",
                table: "user_sessions",
                column: "refresh_token_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_sessions_active_per_company",
                table: "user_sessions",
                columns: new[] { "tenant_id", "company_id", "identity_user_id" },
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_branch_id",
                table: "warehouses",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_establishment_id",
                table: "warehouses",
                column: "establishment_id");

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_tenant_company",
                table: "warehouses",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_warehouses_tenant_branch_code",
                table: "warehouses",
                columns: new[] { "tenant_id", "branch_id", "code" },
                unique: true,
                filter: "code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_warehouses_tenant_branch_main",
                table: "warehouses",
                columns: new[] { "tenant_id", "company_id", "branch_id" },
                unique: true,
                filter: "is_main = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_profile_permissions");

            migrationBuilder.DropTable(
                name: "access_profiles");

            migrationBuilder.DropTable(
                name: "app_features");

            migrationBuilder.DropTable(
                name: "attribute_definitions");

            migrationBuilder.DropTable(
                name: "cash_closing_counts");

            migrationBuilder.DropTable(
                name: "communication_outbox_attachments");

            migrationBuilder.DropTable(
                name: "communication_templates");

            migrationBuilder.DropTable(
                name: "company_financial_destination_audit");

            migrationBuilder.DropTable(
                name: "company_special_tax_responsibilities");

            migrationBuilder.DropTable(
                name: "company_user_branches");

            migrationBuilder.DropTable(
                name: "company_user_preferences");

            migrationBuilder.DropTable(
                name: "config_feature");

            migrationBuilder.DropTable(
                name: "config_global");

            migrationBuilder.DropTable(
                name: "config_module");

            migrationBuilder.DropTable(
                name: "configuration_change_log");

            migrationBuilder.DropTable(
                name: "credit_installments");

            migrationBuilder.DropTable(
                name: "current_stocks");

            migrationBuilder.DropTable(
                name: "doc_type_sri_map",
                schema: "global");

            migrationBuilder.DropTable(
                name: "document_flow_policy");

            migrationBuilder.DropTable(
                name: "document_sequence");

            migrationBuilder.DropTable(
                name: "electronic_document_audit");

            migrationBuilder.DropTable(
                name: "electronic_document_sri_message");

            migrationBuilder.DropTable(
                name: "electronic_documents");

            migrationBuilder.DropTable(
                name: "expense_lines");

            migrationBuilder.DropTable(
                name: "expense_payment_schedules");

            migrationBuilder.DropTable(
                name: "import_batch_files");

            migrationBuilder.DropTable(
                name: "import_batch_issues");

            migrationBuilder.DropTable(
                name: "import_batch_rows");

            migrationBuilder.DropTable(
                name: "inventory_lots");

            migrationBuilder.DropTable(
                name: "inventory_serials");

            migrationBuilder.DropTable(
                name: "issued_withholding_audit");

            migrationBuilder.DropTable(
                name: "issued_withholding_details");

            migrationBuilder.DropTable(
                name: "item_audit");

            migrationBuilder.DropTable(
                name: "item_images");

            migrationBuilder.DropTable(
                name: "item_margin_statuses",
                schema: "global");

            migrationBuilder.DropTable(
                name: "item_special_tax_configurations");

            migrationBuilder.DropTable(
                name: "item_substitutes");

            migrationBuilder.DropTable(
                name: "item_supplier_codes");

            migrationBuilder.DropTable(
                name: "item_unit_conversions");

            migrationBuilder.DropTable(
                name: "item_variant_attributes");

            migrationBuilder.DropTable(
                name: "item_variant_barcodes");

            migrationBuilder.DropTable(
                name: "journal_entry_lines");

            migrationBuilder.DropTable(
                name: "journal_entry_sequences");

            migrationBuilder.DropTable(
                name: "master_bp_carrier_configs");

            migrationBuilder.DropTable(
                name: "master_bp_contacts");

            migrationBuilder.DropTable(
                name: "master_bp_customer_configs");

            migrationBuilder.DropTable(
                name: "master_bp_supplier_classification_configs");

            migrationBuilder.DropTable(
                name: "master_bp_supplier_configs");

            migrationBuilder.DropTable(
                name: "master_company_bp_trading_settings");

            migrationBuilder.DropTable(
                name: "master_customer_categories");

            migrationBuilder.DropTable(
                name: "master_customer_classifications");

            migrationBuilder.DropTable(
                name: "master_customer_credit_ratings");

            migrationBuilder.DropTable(
                name: "master_customer_invoice_formats");

            migrationBuilder.DropTable(
                name: "master_customer_loyalty_tiers");

            migrationBuilder.DropTable(
                name: "master_customer_segments");

            migrationBuilder.DropTable(
                name: "master_supplier_categories");

            migrationBuilder.DropTable(
                name: "master_supplier_good_types");

            migrationBuilder.DropTable(
                name: "master_supplier_ratings");

            migrationBuilder.DropTable(
                name: "master_supplier_risks");

            migrationBuilder.DropTable(
                name: "master_supplier_segments");

            migrationBuilder.DropTable(
                name: "master_supplier_types");

            migrationBuilder.DropTable(
                name: "media_files");

            migrationBuilder.DropTable(
                name: "org_settings");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "payment_application_lines");

            migrationBuilder.DropTable(
                name: "payment_card_details");

            migrationBuilder.DropTable(
                name: "payment_cheque_details");

            migrationBuilder.DropTable(
                name: "payment_transfer_details");

            migrationBuilder.DropTable(
                name: "posting_rule_lines");

            migrationBuilder.DropTable(
                name: "price_list_audit");

            migrationBuilder.DropTable(
                name: "price_list_item_audit");

            migrationBuilder.DropTable(
                name: "price_list_items");

            migrationBuilder.DropTable(
                name: "pricing_rule_audit");

            migrationBuilder.DropTable(
                name: "pricing_rules");

            migrationBuilder.DropTable(
                name: "purchase_communications");

            migrationBuilder.DropTable(
                name: "purchase_credit_note_details");

            migrationBuilder.DropTable(
                name: "purchase_credit_note_tax_summary_lines");

            migrationBuilder.DropTable(
                name: "purchase_invoice_audit");

            migrationBuilder.DropTable(
                name: "purchase_invoice_detail_taxes");

            migrationBuilder.DropTable(
                name: "purchase_line_pvp_audit");

            migrationBuilder.DropTable(
                name: "purchase_payment_schedules");

            migrationBuilder.DropTable(
                name: "purchase_reception_line_additional_fields");

            migrationBuilder.DropTable(
                name: "purchase_reception_line_taxes");

            migrationBuilder.DropTable(
                name: "purchase_return_audit");

            migrationBuilder.DropTable(
                name: "purchase_return_detail_taxes");

            migrationBuilder.DropTable(
                name: "purchase_return_sequence");

            migrationBuilder.DropTable(
                name: "ride_pdf_document");

            migrationBuilder.DropTable(
                name: "sales_invoice_detail_taxes");

            migrationBuilder.DropTable(
                name: "sales_receivable_installments");

            migrationBuilder.DropTable(
                name: "sales_return_audit");

            migrationBuilder.DropTable(
                name: "sales_return_detail_taxes");

            migrationBuilder.DropTable(
                name: "sales_return_refund_allocations");

            migrationBuilder.DropTable(
                name: "security_admin_scope_assignments");

            migrationBuilder.DropTable(
                name: "sri_emission_type",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_error_code",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_ice_rate",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_id_type_usage",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_irbpnr_rate",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_payment_method",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_retention_code",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_settings");

            migrationBuilder.DropTable(
                name: "sri_supplier_type",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_tax_support",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_uom",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_vat_rate",
                schema: "global");

            migrationBuilder.DropTable(
                name: "stock_adjustment_lines");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "stock_transfer_lines");

            migrationBuilder.DropTable(
                name: "supplier_credit_audit");

            migrationBuilder.DropTable(
                name: "supplier_credit_refund_transactions");

            migrationBuilder.DropTable(
                name: "supplier_payment_allocations");

            migrationBuilder.DropTable(
                name: "supplier_payment_sequences");

            migrationBuilder.DropTable(
                name: "system_provider_settings");

            migrationBuilder.DropTable(
                name: "system_setup_state");

            migrationBuilder.DropTable(
                name: "tenant_custom_menus");

            migrationBuilder.DropTable(
                name: "ui_nav_items");

            migrationBuilder.DropTable(
                name: "user_activity");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "attribute_groups");

            migrationBuilder.DropTable(
                name: "communication_outbox");

            migrationBuilder.DropTable(
                name: "company_user_memberships");

            migrationBuilder.DropTable(
                name: "credit_terms");

            migrationBuilder.DropTable(
                name: "doc_type",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_doc_type",
                schema: "global");

            migrationBuilder.DropTable(
                name: "expense_category_nodes");

            migrationBuilder.DropTable(
                name: "expense_documents");

            migrationBuilder.DropTable(
                name: "import_batches");

            migrationBuilder.DropTable(
                name: "issued_withholdings");

            migrationBuilder.DropTable(
                name: "item_packaging_levels");

            migrationBuilder.DropTable(
                name: "barcode_types",
                schema: "global");

            migrationBuilder.DropTable(
                name: "item_variants");

            migrationBuilder.DropTable(
                name: "journal_entries");

            migrationBuilder.DropTable(
                name: "master_bp_locations");

            migrationBuilder.DropTable(
                name: "master_bp_roles");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "sales_invoice_payments");

            migrationBuilder.DropTable(
                name: "posting_rules");

            migrationBuilder.DropTable(
                name: "price_lists");

            migrationBuilder.DropTable(
                name: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropTable(
                name: "purchase_reception_lines");

            migrationBuilder.DropTable(
                name: "purchase_return_details");

            migrationBuilder.DropTable(
                name: "sales_receivables");

            migrationBuilder.DropTable(
                name: "sales_return_details");

            migrationBuilder.DropTable(
                name: "sri_id_type",
                schema: "global");

            migrationBuilder.DropTable(
                name: "stock_adjustments");

            migrationBuilder.DropTable(
                name: "stock_transfers");

            migrationBuilder.DropTable(
                name: "cash_movements");

            migrationBuilder.DropTable(
                name: "supplier_credit_movements");

            migrationBuilder.DropTable(
                name: "supplier_payment_applications");

            migrationBuilder.DropTable(
                name: "supplier_payment_methods");

            migrationBuilder.DropTable(
                name: "ui_nav_groups");

            migrationBuilder.DropTable(
                name: "identity_users");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "accounting_periods");

            migrationBuilder.DropTable(
                name: "purchase_credit_notes");

            migrationBuilder.DropTable(
                name: "purchase_invoice_tax_summaries");

            migrationBuilder.DropTable(
                name: "purchase_invoice_details");

            migrationBuilder.DropTable(
                name: "sales_invoice_details");

            migrationBuilder.DropTable(
                name: "sales_returns");

            migrationBuilder.DropTable(
                name: "inventory_adjustment_reasons");

            migrationBuilder.DropTable(
                name: "supplier_credits");

            migrationBuilder.DropTable(
                name: "accounts_payable_installments");

            migrationBuilder.DropTable(
                name: "company_financial_destinations");

            migrationBuilder.DropTable(
                name: "payment_methods");

            migrationBuilder.DropTable(
                name: "supplier_payments");

            migrationBuilder.DropTable(
                name: "items");

            migrationBuilder.DropTable(
                name: "sales_invoices");

            migrationBuilder.DropTable(
                name: "purchase_returns");

            migrationBuilder.DropTable(
                name: "accounts_payables");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "brands");

            migrationBuilder.DropTable(
                name: "item_category_nodes");

            migrationBuilder.DropTable(
                name: "item_types");

            migrationBuilder.DropTable(
                name: "cash_sessions");

            migrationBuilder.DropTable(
                name: "purchase_reception_documents");

            migrationBuilder.DropTable(
                name: "cash_registers");

            migrationBuilder.DropTable(
                name: "purchase_invoices");

            migrationBuilder.DropTable(
                name: "emission_point");

            migrationBuilder.DropTable(
                name: "master_business_partners");

            migrationBuilder.DropTable(
                name: "master_payment_terms");

            migrationBuilder.DropTable(
                name: "warehouses");

            migrationBuilder.DropTable(
                name: "legal_entity_type",
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
                name: "sri_tax_regime",
                schema: "global");

            migrationBuilder.DropTable(
                name: "tenants");

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
