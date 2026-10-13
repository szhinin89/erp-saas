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
    // Código de barras real de la presentación de ESTA fila (opcional; nunca el código del
    // proveedor). Factor 1 → código del Item (requiere tipo); factor > 1 → código de la presentación.
    barcode: z.string(),
    barcodeType: z.string(),
    saleVatCode: z.string(),
    purchaseVatCode: z.string(),
    exciseTaxCode: z.string(),
    // PVP final de venta POR UNIDAD BASE tal como lo ingresa el usuario: con IVA si la tarifa de
    // venta lo tiene; el precio sin IVA que se persiste (BaseSalePrice) se deriva al enviar.
    salePriceGross: z.number().finite().nullable(),
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
      require("purchaseVatCode", row.purchaseVatCode.length > 0, "Seleccione el IVA de compra.");
      require("saleVatCode", row.saleVatCode.length > 0, "Seleccione el IVA de venta.");
      require("salePriceGross", row.salePriceGross != null && row.salePriceGross > 0, "Ingrese el PVP final (mayor a cero).");
    }
    if (row.mode === "link")
      require("linkItemId", row.linkItemId.length > 0, "Seleccione el producto existente.");
    if (row.mode === "presentation")
      require("parentRowId", row.parentRowId.length > 0, "Seleccione el producto nuevo al que pertenece.");
    if (row.mode !== "link") {
      const barcode = row.barcode.trim();
      const isUnit = row.presentationFactor == null || row.presentationFactor === 1;
      require("barcode", barcode.length <= 100, "El código de barras no puede exceder 100 caracteres.");
      require(
        "barcodeType",
        !barcode || !isUnit || row.barcodeType.length > 0,
        "Seleccione el tipo del código de barras de la unidad.",
      );
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
    // Presentación destino (producto nuevo + factor): varias filas de la misma presentación
    // pueden repetir su código; un mismo código en dos presentaciones, no.
    const target = (r: PendingProductRow) =>
      `${r.mode === "create" ? r.rowId : r.parentRowId}:${r.presentationFactor ?? 1}`;
    const targetsByBarcode = new Map<string, Set<string>>();
    const barcodesByTarget = new Map<string, Set<string>>();
    // FASE 5 PURCHASE-UX — cada presentación de un producto nuevo (unidad base + una por factor)
    // necesita su propia unidad de medida (índice único item_id + uom_code). Producto = fila que lo
    // crea; unidad base = su "Unidad base".
    const owner = (r: PendingProductRow) => (r.mode === "create" ? r.rowId : r.parentRowId);
    const baseUomByOwner = new Map(
      selected.filter((r) => r.mode === "create").map((r) => [r.rowId, r.defaultUomCode.trim().toUpperCase()]),
    );
    const isPresentation = (r: PendingProductRow) =>
      r.mode !== "link" && r.presentationFactor != null && r.presentationFactor !== 1;
    const factorsByOwnerUom = new Map<string, Set<number>>();
    for (const r of selected) {
      const uom = r.presentationUomCode.trim().toUpperCase();
      if (!isPresentation(r) || !uom) continue;
      const key = `${owner(r)}:${uom}`;
      factorsByOwnerUom.set(key, (factorsByOwnerUom.get(key) ?? new Set()).add(r.presentationFactor as number));
    }
    for (const r of selected) {
      const sku = r.sku.trim().toUpperCase();
      if (r.mode === "create" && sku) skuCount.set(sku, (skuCount.get(sku) ?? 0) + 1);
      const barcode = r.barcode.trim();
      if (r.mode === "link" || !barcode) continue;
      targetsByBarcode.set(barcode, (targetsByBarcode.get(barcode) ?? new Set()).add(target(r)));
      barcodesByTarget.set(target(r), (barcodesByTarget.get(target(r)) ?? new Set()).add(barcode));
    }
    form.rows.forEach((r, index) => {
      if (!r.selected) return;
      if (r.mode === "presentation" && r.parentRowId && !createIds.has(r.parentRowId))
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["rows", index, "parentRowId"],
          message: "El producto nuevo elegido debe estar seleccionado para crearse.",
        });
      if (r.mode === "link") return;
      const presentationUom = r.presentationUomCode.trim().toUpperCase();
      if (isPresentation(r) && presentationUom) {
        const name = r.presentationName.trim() || `x${r.presentationFactor}`;
        if (presentationUom === baseUomByOwner.get(owner(r)))
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            path: ["rows", index, "presentationUomCode"],
            message: `La presentación "${name}" no puede usar la misma unidad que la unidad base. Cada presentación del producto necesita su propia unidad: elija otra (p. ej. Paquete o Caja).`,
          });
        else if ((factorsByOwnerUom.get(`${owner(r)}:${presentationUom}`)?.size ?? 0) > 1)
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            path: ["rows", index, "presentationUomCode"],
            message: `Otra presentación de este producto ya usa esa unidad. Cada presentación necesita su propia unidad: elija otra (p. ej. Paquete o Caja).`,
          });
      }
      const barcode = r.barcode.trim();
      if (barcode && (targetsByBarcode.get(barcode)?.size ?? 0) > 1)
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["rows", index, "barcode"], message: "El código de barras está repetido en el lote." });
      else if (barcode && (barcodesByTarget.get(target(r))?.size ?? 0) > 1)
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["rows", index, "barcode"], message: "Otra línea de esta misma presentación tiene un código de barras distinto." });
      if (r.mode !== "create") return;
      if ((skuCount.get(r.sku.trim().toUpperCase()) ?? 0) > 1)
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["rows", index, "sku"], message: "El SKU está repetido en el lote." });
    });
  });

export type PendingProductsFormValues = z.infer<typeof pendingProductsFormSchema>;
