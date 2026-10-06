using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ItemCompanyScopeAndInventoryNature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_item_packaging_levels_items_item_id",
                table: "item_packaging_levels");

            migrationBuilder.DropForeignKey(
                name: "FK_item_supplier_codes_items_item_id",
                table: "item_supplier_codes");

            migrationBuilder.DropForeignKey(
                name: "FK_item_variant_barcodes_item_variants_variant_id",
                table: "item_variant_barcodes");

            migrationBuilder.DropForeignKey(
                name: "FK_item_variants_items_item_id",
                table: "item_variants");

            migrationBuilder.DropIndex(
                name: "uq_item_variants_tenant_sku",
                table: "item_variants");

            migrationBuilder.DropIndex(
                name: "IX_item_variant_barcodes_variant_id",
                table: "item_variant_barcodes");

            migrationBuilder.DropIndex(
                name: "uq_item_variant_barcode_tenant_code",
                table: "item_variant_barcodes");

            migrationBuilder.DropIndex(
                name: "uq_item_supplier_codes_tenant_supplier_code",
                table: "item_supplier_codes");

            migrationBuilder.DropIndex(
                name: "ix_item_packaging_levels_barcode",
                table: "item_packaging_levels");

            migrationBuilder.RenameColumn(
                name: "tracks_stock",
                table: "items",
                newName: "stock_control_enabled");

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "nature",
                table: "items",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "item_variants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "item_variant_barcodes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "item_supplier_codes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "item_packaging_levels",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Development ownership is resolved only from real Company references (no arbitrary owner).
            migrationBuilder.Sql("""
                WITH owners AS (
                    SELECT l.item_id, h.company_id FROM sales_invoice_details l JOIN sales_invoices h ON h.id=l.invoice_id WHERE l.item_id IS NOT NULL
                    UNION SELECT l.item_id, h.company_id FROM purchase_invoice_details l JOIN purchase_invoices h ON h.id=l.purchase_invoice_id WHERE l.item_id IS NOT NULL
                    UNION SELECT product_id, company_id FROM current_stocks
                    UNION SELECT item_id, company_id FROM price_list_items
                ), resolved AS (
                    SELECT item_id, (array_agg(company_id))[1] AS company_id FROM owners GROUP BY item_id HAVING count(DISTINCT company_id)=1
                ) UPDATE items i SET company_id=r.company_id FROM resolved r WHERE r.item_id=i.id;
                UPDATE items i SET company_id=c.id FROM company c
                WHERE i.company_id='00000000-0000-0000-0000-000000000000' AND c.tenant_id=i.tenant_id
                  AND (SELECT count(*) FROM company c2 WHERE c2.tenant_id=i.tenant_id)=1;
                DO $$ BEGIN
                    IF EXISTS(SELECT 1 FROM items WHERE company_id='00000000-0000-0000-0000-000000000000') THEN
                        RAISE EXCEPTION 'A1 STOP: Item ownership is ambiguous or unresolved';
                    END IF;
                    IF EXISTS(SELECT 1 FROM sales_invoice_details l JOIN sales_invoices h ON h.id=l.invoice_id JOIN items i ON i.id=l.item_id WHERE h.company_id<>i.company_id)
                       OR EXISTS(SELECT 1 FROM purchase_invoice_details l JOIN purchase_invoices h ON h.id=l.purchase_invoice_id JOIN items i ON i.id=l.item_id WHERE h.company_id<>i.company_id) THEN
                        RAISE EXCEPTION 'A1 STOP: Item shared by multiple Companies';
                    END IF;
                END $$;
                UPDATE items SET nature='Product';
                UPDATE item_variants c SET company_id=i.company_id FROM items i WHERE i.id=c.item_id;
                UPDATE item_variant_barcodes c SET company_id=i.company_id FROM items i WHERE i.id=c.item_id;
                UPDATE item_supplier_codes c SET company_id=i.company_id FROM items i WHERE i.id=c.item_id;
                UPDATE item_packaging_levels c SET company_id=i.company_id FROM items i WHERE i.id=c.item_id;
                ALTER TABLE items ALTER COLUMN company_id DROP DEFAULT, ALTER COLUMN nature DROP DEFAULT;
                ALTER TABLE item_variants ALTER COLUMN company_id DROP DEFAULT;
                ALTER TABLE item_variant_barcodes ALTER COLUMN company_id DROP DEFAULT;
                ALTER TABLE item_supplier_codes ALTER COLUMN company_id DROP DEFAULT;
                ALTER TABLE item_packaging_levels ALTER COLUMN company_id DROP DEFAULT;
                DROP INDEX IF EXISTS uq_items_tenant_sku;
                CREATE UNIQUE INDEX uq_items_tenant_company_sku ON items(tenant_id, company_id, sku);
                ALTER TABLE items ADD CONSTRAINT ck_items_nature CHECK(nature IN ('Product','Service'));
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_items_id_tenant_id_company_id",
                table: "items",
                columns: new[] { "id", "tenant_id", "company_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_item_variants_id_tenant_id_company_id",
                table: "item_variants",
                columns: new[] { "id", "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "IX_price_list_items_item_id_tenant_id_company_id",
                table: "price_list_items",
                columns: new[] { "item_id", "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "IX_items_company_id",
                table: "items",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_variants_item_id_tenant_id_company_id",
                table: "item_variants",
                columns: new[] { "item_id", "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_item_variants_tenant_company_sku",
                table: "item_variants",
                columns: new[] { "tenant_id", "company_id", "sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_variant_barcodes_item_id_tenant_id_company_id",
                table: "item_variant_barcodes",
                columns: new[] { "item_id", "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "IX_item_variant_barcodes_variant_id_tenant_id_company_id",
                table: "item_variant_barcodes",
                columns: new[] { "variant_id", "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_item_variant_barcode_tenant_company_code",
                table: "item_variant_barcodes",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_supplier_codes_item_id_tenant_id_company_id",
                table: "item_supplier_codes",
                columns: new[] { "item_id", "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_item_supplier_codes_tenant_company_supplier_code",
                table: "item_supplier_codes",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_packaging_levels_barcode",
                table: "item_packaging_levels",
                columns: new[] { "tenant_id", "company_id", "barcode" },
                unique: true,
                filter: "barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_item_packaging_levels_item_id_tenant_id_company_id",
                table: "item_packaging_levels",
                columns: new[] { "item_id", "tenant_id", "company_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_item_packaging_levels_items_item_id_tenant_id_company_id",
                table: "item_packaging_levels",
                columns: new[] { "item_id", "tenant_id", "company_id" },
                principalTable: "items",
                principalColumns: new[] { "id", "tenant_id", "company_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_item_supplier_codes_items_item_id_tenant_id_company_id",
                table: "item_supplier_codes",
                columns: new[] { "item_id", "tenant_id", "company_id" },
                principalTable: "items",
                principalColumns: new[] { "id", "tenant_id", "company_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_item_variant_barcodes_item_variants_variant_id_tenant_id_co~",
                table: "item_variant_barcodes",
                columns: new[] { "variant_id", "tenant_id", "company_id" },
                principalTable: "item_variants",
                principalColumns: new[] { "id", "tenant_id", "company_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_item_variant_barcodes_items_item_id_tenant_id_company_id",
                table: "item_variant_barcodes",
                columns: new[] { "item_id", "tenant_id", "company_id" },
                principalTable: "items",
                principalColumns: new[] { "id", "tenant_id", "company_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_item_variants_items_item_id_tenant_id_company_id",
                table: "item_variants",
                columns: new[] { "item_id", "tenant_id", "company_id" },
                principalTable: "items",
                principalColumns: new[] { "id", "tenant_id", "company_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_items_company_company_id",
                table: "items",
                column: "company_id",
                principalTable: "company",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_price_list_items_items_item_id_tenant_id_company_id",
                table: "price_list_items",
                columns: new[] { "item_id", "tenant_id", "company_id" },
                principalTable: "items",
                principalColumns: new[] { "id", "tenant_id", "company_id" },
                onDelete: ReferentialAction.Restrict);
            // A barcode has one owner across variant and packaging tables within a Company.
            // Advisory locking makes the cross-table constraint safe under concurrent inserts.
            migrationBuilder.Sql("""
                CREATE FUNCTION a1_item_barcode_unique() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE barcode_value text;
                BEGIN
                    IF TG_TABLE_NAME='item_variant_barcodes' THEN barcode_value=NEW.code; ELSE barcode_value=NEW.barcode; END IF;
                    IF barcode_value IS NULL THEN RETURN NEW; END IF;
                    PERFORM pg_advisory_xact_lock(hashtextextended(NEW.tenant_id::text || NEW.company_id::text || barcode_value, 0));
                    IF EXISTS(SELECT 1 FROM item_variant_barcodes b WHERE b.tenant_id=NEW.tenant_id AND b.company_id=NEW.company_id AND b.code=barcode_value AND (TG_TABLE_NAME<>'item_variant_barcodes' OR b.id<>NEW.id))
                       OR EXISTS(SELECT 1 FROM item_packaging_levels p WHERE p.tenant_id=NEW.tenant_id AND p.company_id=NEW.company_id AND p.barcode=barcode_value AND (TG_TABLE_NAME<>'item_packaging_levels' OR p.id<>NEW.id)) THEN
                        RAISE EXCEPTION 'Barcode already belongs to this Company' USING ERRCODE='23505', CONSTRAINT='uq_item_barcode_tenant_company';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER a1_variant_barcode_unique BEFORE INSERT OR UPDATE ON item_variant_barcodes FOR EACH ROW EXECUTE FUNCTION a1_item_barcode_unique();
                CREATE TRIGGER a1_packaging_barcode_unique BEFORE INSERT OR UPDATE ON item_packaging_levels FOR EACH ROW EXECUTE FUNCTION a1_item_barcode_unique();

                CREATE FUNCTION a1_inventory_product_scope() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM items i WHERE i.id=NEW.product_id AND i.tenant_id=NEW.tenant_id AND i.company_id=NEW.company_id AND i.nature='Product') THEN
                        RAISE EXCEPTION 'Inventory requires a Product owned by this Company' USING ERRCODE='23514', CONSTRAINT='ck_inventory_item_nature_scope';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER a1_stock_product_scope BEFORE INSERT OR UPDATE ON current_stocks FOR EACH ROW EXECUTE FUNCTION a1_inventory_product_scope();
                CREATE TRIGGER a1_movement_product_scope BEFORE INSERT OR UPDATE ON stock_movements FOR EACH ROW EXECUTE FUNCTION a1_inventory_product_scope();
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER a1_variant_barcode_unique ON item_variant_barcodes;
                DROP TRIGGER a1_packaging_barcode_unique ON item_packaging_levels;
                DROP FUNCTION a1_item_barcode_unique();
                DROP TRIGGER a1_stock_product_scope ON current_stocks;
                DROP TRIGGER a1_movement_product_scope ON stock_movements;
                DROP FUNCTION a1_inventory_product_scope();
                DROP INDEX uq_items_tenant_company_sku;
                ALTER TABLE items DROP CONSTRAINT ck_items_nature;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_item_packaging_levels_items_item_id_tenant_id_company_id",
                table: "item_packaging_levels");

            migrationBuilder.DropForeignKey(
                name: "FK_item_supplier_codes_items_item_id_tenant_id_company_id",
                table: "item_supplier_codes");

            migrationBuilder.DropForeignKey(
                name: "FK_item_variant_barcodes_item_variants_variant_id_tenant_id_co~",
                table: "item_variant_barcodes");

            migrationBuilder.DropForeignKey(
                name: "FK_item_variant_barcodes_items_item_id_tenant_id_company_id",
                table: "item_variant_barcodes");

            migrationBuilder.DropForeignKey(
                name: "FK_item_variants_items_item_id_tenant_id_company_id",
                table: "item_variants");

            migrationBuilder.DropForeignKey(
                name: "FK_items_company_company_id",
                table: "items");

            migrationBuilder.DropForeignKey(
                name: "FK_price_list_items_items_item_id_tenant_id_company_id",
                table: "price_list_items");

            migrationBuilder.DropIndex(
                name: "IX_price_list_items_item_id_tenant_id_company_id",
                table: "price_list_items");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_items_id_tenant_id_company_id",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_company_id",
                table: "items");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_item_variants_id_tenant_id_company_id",
                table: "item_variants");

            migrationBuilder.DropIndex(
                name: "IX_item_variants_item_id_tenant_id_company_id",
                table: "item_variants");

            migrationBuilder.DropIndex(
                name: "uq_item_variants_tenant_company_sku",
                table: "item_variants");

            migrationBuilder.DropIndex(
                name: "IX_item_variant_barcodes_item_id_tenant_id_company_id",
                table: "item_variant_barcodes");

            migrationBuilder.DropIndex(
                name: "IX_item_variant_barcodes_variant_id_tenant_id_company_id",
                table: "item_variant_barcodes");

            migrationBuilder.DropIndex(
                name: "uq_item_variant_barcode_tenant_company_code",
                table: "item_variant_barcodes");

            migrationBuilder.DropIndex(
                name: "IX_item_supplier_codes_item_id_tenant_id_company_id",
                table: "item_supplier_codes");

            migrationBuilder.DropIndex(
                name: "uq_item_supplier_codes_tenant_company_supplier_code",
                table: "item_supplier_codes");

            migrationBuilder.DropIndex(
                name: "ix_item_packaging_levels_barcode",
                table: "item_packaging_levels");

            migrationBuilder.DropIndex(
                name: "IX_item_packaging_levels_item_id_tenant_id_company_id",
                table: "item_packaging_levels");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "nature",
                table: "items");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "item_variants");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "item_variant_barcodes");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "item_supplier_codes");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "item_packaging_levels");

            migrationBuilder.RenameColumn(
                name: "stock_control_enabled",
                table: "items",
                newName: "tracks_stock");

            migrationBuilder.CreateIndex(
                name: "uq_item_variants_tenant_sku",
                table: "item_variants",
                columns: new[] { "tenant_id", "sku" },
                unique: true);

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
                name: "uq_item_supplier_codes_tenant_supplier_code",
                table: "item_supplier_codes",
                columns: new[] { "tenant_id", "supplier_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_packaging_levels_barcode",
                table: "item_packaging_levels",
                column: "barcode",
                filter: "barcode IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_item_packaging_levels_items_item_id",
                table: "item_packaging_levels",
                column: "item_id",
                principalTable: "items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_item_supplier_codes_items_item_id",
                table: "item_supplier_codes",
                column: "item_id",
                principalTable: "items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_item_variant_barcodes_item_variants_variant_id",
                table: "item_variant_barcodes",
                column: "variant_id",
                principalTable: "item_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_item_variants_items_item_id",
                table: "item_variants",
                column: "item_id",
                principalTable: "items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
