using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Items_CanonicalModel_v1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_colors");

            migrationBuilder.DropTable(
                name: "product_custom_fields");

            migrationBuilder.DropTable(
                name: "product_dimensions");

            migrationBuilder.DropTable(
                name: "product_features");

            migrationBuilder.DropTable(
                name: "product_sizes");

            migrationBuilder.DropTable(
                name: "product_supplier_codes");

            migrationBuilder.DropTable(
                name: "product_tariff_details");

            migrationBuilder.DropColumn(
                name: "base_color",
                table: "products");

            migrationBuilder.DropColumn(
                name: "handles_tariff",
                table: "products");

            migrationBuilder.DropColumn(
                name: "has_multiple_colors",
                table: "products");

            migrationBuilder.DropColumn(
                name: "has_sizes",
                table: "products");

            migrationBuilder.CreateTable(
                name: "attribute_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cost_layers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    layer_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_document_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entry_qty = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    total_cost = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    remaining_qty = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    is_exhausted = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_layers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "digital_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    delivery_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    max_downloads = table.Column<int>(type: "integer", nullable: true),
                    validity_days = table.Column<int>(type: "integer", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_digital_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    short_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    purchase_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    item_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observations = table.Column<string>(type: "text", nullable: true),
                    category_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    applies_vat_on_sale = table.Column<bool>(type: "boolean", nullable: false),
                    applies_vat_on_purchase = table.Column<bool>(type: "boolean", nullable: false),
                    applies_excise_tax = table.Column<bool>(type: "boolean", nullable: false),
                    sale_vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    purchase_vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    excise_tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    vat_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchase_vat_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    excise_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sri_service_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
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
                    specifications = table.Column<string>(type: "jsonb", nullable: false),
                    marketing_attributes = table.Column<string>(type: "jsonb", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kit_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lots",
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
                    initial_qty = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    current_qty = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    receipt_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "price_lists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    price_includes_vat = table.Column<bool>(type: "boolean", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    target = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_lists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "serial_numbers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    serial = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    acquired_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sold_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    document_ref = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    receipt_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serial_numbers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    default_uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    lead_time_days = table.Column<int>(type: "integer", nullable: false),
                    min_order_qty = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_supplier_items", x => x.id);
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
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "digital_deliverables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    digital_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    storage_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_digital_deliverables", x => x.id);
                    table.ForeignKey(
                        name: "FK_digital_deliverables_digital_items_digital_item_id",
                        column: x => x.digital_item_id,
                        principalTable: "digital_items",
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
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "item_substitutes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    substitute_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "kit_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_optional = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kit_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_kit_lines_kits_kit_id",
                        column: x => x.kit_id,
                        principalTable: "kits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "price_list_discounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    discount_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    discount_value = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    min_qty = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_list_discounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_price_list_discounts_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "price_list_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    price = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    min_qty = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_list_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_price_list_entries_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_item_prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_qty = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_item_prices", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_item_prices_supplier_items_supplier_item_id",
                        column: x => x.supplier_item_id,
                        principalTable: "supplier_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_variant_attributes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_variant_barcodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_variant_barcodes_item_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "item_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attribute_definitions_group_id",
                table: "attribute_definitions",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_definitions_variant_axis",
                table: "attribute_definitions",
                columns: new[] { "subscriber_id", "is_variant_axis" });

            migrationBuilder.CreateIndex(
                name: "uq_attribute_definitions_subscriber_group_code",
                table: "attribute_definitions",
                columns: new[] { "subscriber_id", "group_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_attribute_groups_subscriber_code",
                table: "attribute_groups",
                columns: new[] { "subscriber_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cost_layers_fifo",
                table: "cost_layers",
                columns: new[] { "subscriber_id", "company_id", "item_id", "warehouse_id", "is_exhausted", "layer_date" });

            migrationBuilder.CreateIndex(
                name: "ix_cost_layers_item",
                table: "cost_layers",
                columns: new[] { "subscriber_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_digital_deliverables_digital_item_id",
                table: "digital_deliverables",
                column: "digital_item_id");

            migrationBuilder.CreateIndex(
                name: "uq_digital_item",
                table: "digital_items",
                column: "item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_images_item",
                table: "item_images",
                column: "item_id");

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
                name: "uq_item_substitute",
                table: "item_substitutes",
                columns: new[] { "item_id", "substitute_item_id" },
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
                name: "ix_item_variant_barcodes_code",
                table: "item_variant_barcodes",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "IX_item_variant_barcodes_variant_id",
                table: "item_variant_barcodes",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "uq_item_variant_barcode_code",
                table: "item_variant_barcodes",
                columns: new[] { "item_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_variants_item",
                table: "item_variants",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "uq_item_variants_item_sku",
                table: "item_variants",
                columns: new[] { "item_id", "sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_items_subscriber",
                table: "items",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "ix_items_subscriber_category",
                table: "items",
                columns: new[] { "subscriber_id", "category_node_id" });

            migrationBuilder.CreateIndex(
                name: "ix_items_subscriber_type",
                table: "items",
                columns: new[] { "subscriber_id", "item_type" });

            // uq_kit_component created below as expression index (COALESCE for nullable variant)

            migrationBuilder.CreateIndex(
                name: "uq_kit_item",
                table: "kits",
                column: "item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lots_expiration_active",
                table: "lots",
                column: "expiration_date",
                filter: "expiration_date IS NOT NULL AND status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_lots_subscriber_item",
                table: "lots",
                columns: new[] { "subscriber_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "uq_lot_number",
                table: "lots",
                columns: new[] { "subscriber_id", "company_id", "item_id", "lot_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_price_list_discounts_price_list_id",
                table: "price_list_discounts",
                column: "price_list_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_list_entries_item",
                table: "price_list_entries",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_list_entries_list_item",
                table: "price_list_entries",
                columns: new[] { "price_list_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_price_lists_default",
                table: "price_lists",
                columns: new[] { "subscriber_id", "company_id", "is_default" });

            migrationBuilder.CreateIndex(
                name: "uq_price_lists_subscriber_company_code",
                table: "price_lists",
                columns: new[] { "subscriber_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_serial_numbers_status",
                table: "serial_numbers",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_serial_numbers_subscriber_item",
                table: "serial_numbers",
                columns: new[] { "subscriber_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "uq_serial_number",
                table: "serial_numbers",
                columns: new[] { "subscriber_id", "company_id", "item_id", "serial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_item_prices_supplier_item_id",
                table: "supplier_item_prices",
                column: "supplier_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_items_item",
                table: "supplier_items",
                columns: new[] { "subscriber_id", "item_id" });

            // Expression unique index: COALESCE handles NULL variant_id correctly
            // (PostgreSQL treats NULLs as distinct in regular unique indexes)
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX uq_supplier_item
                ON supplier_items (subscriber_id, supplier_id, item_id, COALESCE(variant_id, '00000000-0000-0000-0000-000000000000'::uuid));
            ");

            // Expression unique index for kit_lines — same NULL variant issue
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX uq_kit_component_expr
                ON kit_lines (kit_id, component_item_id, COALESCE(component_variant_id, '00000000-0000-0000-0000-000000000000'::uuid));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attribute_definitions");

            migrationBuilder.DropTable(
                name: "cost_layers");

            migrationBuilder.DropTable(
                name: "digital_deliverables");

            migrationBuilder.DropTable(
                name: "item_images");

            migrationBuilder.DropTable(
                name: "item_packaging_levels");

            migrationBuilder.DropTable(
                name: "item_substitutes");

            migrationBuilder.DropTable(
                name: "item_unit_conversions");

            migrationBuilder.DropTable(
                name: "item_variant_attributes");

            migrationBuilder.DropTable(
                name: "item_variant_barcodes");

            migrationBuilder.DropTable(
                name: "kit_lines");

            migrationBuilder.DropTable(
                name: "lots");

            migrationBuilder.DropTable(
                name: "price_list_discounts");

            migrationBuilder.DropTable(
                name: "price_list_entries");

            migrationBuilder.DropTable(
                name: "serial_numbers");

            migrationBuilder.DropTable(
                name: "supplier_item_prices");

            migrationBuilder.DropTable(
                name: "attribute_groups");

            migrationBuilder.DropTable(
                name: "digital_items");

            migrationBuilder.DropTable(
                name: "item_variants");

            migrationBuilder.DropTable(
                name: "kits");

            migrationBuilder.DropTable(
                name: "price_lists");

            migrationBuilder.DropTable(
                name: "supplier_items");

            migrationBuilder.DropTable(
                name: "items");

            migrationBuilder.AddColumn<string>(
                name: "base_color",
                table: "products",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "handles_tariff",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "has_multiple_colors",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "has_sizes",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "product_colors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hex_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    field_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    field_type = table.Column<int>(type: "integer", nullable: false),
                    field_value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
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
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false)
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
                name: "product_sizes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                name: "product_supplier_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    origin_country = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tariff_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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
                name: "ix_product_sizes_product_id",
                table: "product_sizes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_sizes_subscriber_name",
                table: "product_sizes",
                columns: new[] { "subscriber_id", "name" });

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
        }
    }
}
