import { z } from "zod";

/**
 * COMPRAS-METODO-ZH-01B — resolución masiva de líneas XML pendientes.
 * - create: la línea crea un producto nuevo (su código queda en la presentación indicada).
 * - link: la línea se vincula a un producto existente (con su presentación).
 * - presentation: la línea es otra presentación (otro código del proveedor) de un producto nuevo
 *   creado por otra fila del mismo lote (p. ej. unidad + caja x12).
 */
export const PENDING_PRODUCT_MODES = ["create", "link", "presentation"] as const;
export type PendingProductMode = (typeof PENDING_PRODUCT_MODES)[number];

const SKU_PATTERN = /^[A-Za-z0-9\-_.]+$/;

export const pendingProductRowSchema = z
  .object({
    rowId: z.string(),
    selected: z.boolean(),
    mode: z.enum(PENDING_PRODUCT_MODES),
    // Producto nuevo — mismas reglas que el editor de Items (ItemEditorModal/itemEditorSchema).
    sku: z.string(),
    shortName: z.string(),
    description: z.string(),
    itemTypeId: z.string(),
    brandId: z.string(),
    categoryNodeId: z.string(),
    defaultUomCode: z.string(),
    barcode: z.string(),
    barcodeType: z.string(),
    saleVatCode: z.string(),
    purchaseVatCode: z.string(),
    exciseTaxCode: z.string(),
    salePrice: z.number().finite().nullable(),
    // Presentación que entrega el proveedor con ESTE código (1 = unidad base).
    presentationFactor: z.number().finite().nullable(),
    presentationName: z.string(),
    presentationUomCode: z.string(),
    // Vincular existente.
    linkItemId: z.string(),
    linkItemLabel: z.string(),
    linkPackagingLevelId: z.string(),
    // Otra presentación de un producto nuevo del lote.
    parentRowId: z.string(),
  })
  .superRefine((row, ctx) => {
    if (!row.selected) return;
    const require = (path: keyof typeof row, ok: boolean, message: string) => {
      if (!ok) ctx.addIssue({ code: z.ZodIssueCode.custom, path: [path], message });
    };
    const needsPresentationDetail =
      row.mode !== "link" && row.presentationFactor != null && row.presentationFactor !== 1;

    if (row.mode === "create") {
      const sku = row.sku.trim();
      require("sku", sku.length > 0, "El SKU es obligatorio.");
      require("sku", sku.length <= 50, "El SKU no puede exceder 50 caracteres.");
      require(
        "sku",
        sku.length === 0 || SKU_PATTERN.test(sku),
        "El SKU solo puede contener letras, números, guiones, puntos y guiones bajos.",
      );
      require("shortName", row.shortName.trim().length > 0, "El nombre es obligatorio.");
      require("shortName", row.shortName.trim().length <= 50, "El nombre no puede exceder 50 caracteres.");
      require("description", row.description.trim().length > 0, "La descripción es obligatoria.");
      require("description", row.description.trim().length <= 254, "La descripción no puede exceder 254 caracteres.");
      require("itemTypeId", row.itemTypeId.length > 0, "El tipo de producto es obligatorio.");
      require("brandId", row.brandId.length > 0, "La marca es obligatoria.");
      require("categoryNodeId", row.categoryNodeId.length > 0, "La categoría es obligatoria.");
      require("defaultUomCode", row.defaultUomCode.length > 0, "La unidad de medida es obligatoria.");
      require("barcode", row.barcode.trim().length > 0, "El código de barras es obligatorio.");
      require("barcode", row.barcode.trim().length <= 100, "El código de barras no puede exceder 100 caracteres.");
      require("barcodeType", row.barcodeType.length > 0, "El tipo de código de barras es obligatorio.");
      require("purchaseVatCode", row.purchaseVatCode.length > 0, "Seleccione el IVA de compra.");
      require("saleVatCode", row.saleVatCode.length > 0, "Seleccione el IVA de venta.");
      require("salePrice", row.salePrice != null && row.salePrice > 0, "El precio de venta debe ser mayor a cero.");
    }
    if (row.mode === "link")
      require("linkItemId", row.linkItemId.length > 0, "Seleccione el producto existente.");
    if (row.mode === "presentation")
      require("parentRowId", row.parentRowId.length > 0, "Seleccione el producto nuevo al que pertenece.");
    if (row.mode !== "link") {
      require(
        "presentationFactor",
        row.presentationFactor != null && row.presentationFactor > 0,
        "La cantidad por presentación debe ser mayor a cero.",
      );
      require("presentationName", !needsPresentationDetail || row.presentationName.trim().length > 0, "Indique el nombre de la presentación.");
      require("presentationUomCode", !needsPresentationDetail || row.presentationUomCode.length > 0, "Indique la unidad de la presentación.");
    }
  });

export type PendingProductRow = z.infer<typeof pendingProductRowSchema>;

export const pendingProductsFormSchema = z
  .object({ rows: z.array(pendingProductRowSchema) })
  .superRefine((form, ctx) => {
    const selected = form.rows.filter((r) => r.selected);
    const createIds = new Set(selected.filter((r) => r.mode === "create").map((r) => r.rowId));
    const skuCount = new Map<string, number>();
    const barcodeCount = new Map<string, number>();
    for (const r of selected.filter((r) => r.mode === "create")) {
      const sku = r.sku.trim().toUpperCase();
      const barcode = r.barcode.trim();
      if (sku) skuCount.set(sku, (skuCount.get(sku) ?? 0) + 1);
      if (barcode) barcodeCount.set(barcode, (barcodeCount.get(barcode) ?? 0) + 1);
    }
    form.rows.forEach((r, index) => {
      if (!r.selected) return;
      if (r.mode === "presentation" && r.parentRowId && !createIds.has(r.parentRowId))
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["rows", index, "parentRowId"],
          message: "El producto nuevo elegido debe estar seleccionado para crearse.",
        });
      if (r.mode !== "create") return;
      if ((skuCount.get(r.sku.trim().toUpperCase()) ?? 0) > 1)
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["rows", index, "sku"], message: "El SKU está repetido en el lote." });
      if ((barcodeCount.get(r.barcode.trim()) ?? 0) > 1)
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["rows", index, "barcode"], message: "El código de barras está repetido en el lote." });
    });
  });

export type PendingProductsFormValues = z.infer<typeof pendingProductsFormSchema>;
