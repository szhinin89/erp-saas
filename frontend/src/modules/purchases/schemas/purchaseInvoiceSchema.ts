import { hasInvalidBreakdown } from "../utils/purchaseXmlBreakdown";
import { z } from "zod";
import type { PurchaseItemContextDto } from "../api/purchaseService";

type TFunction = (key: string, fallback: string) => string;

const passthroughT: TFunction = (_key, fallback) => fallback;

// ── Line sub-schema ────────────────────────────────────────────────────
export const createPurchaseLineSchema = (t: TFunction) => z.object({
  _key: z.number(),
  // PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — Id de la línea guardada (viaja como `lineId` para que
  // sus distribuciones de costos sigan referenciándola). Ausente en líneas nuevas o duplicadas.
  lineId: z.string().optional(),
  xmlSource: z.custom<import("../api/purchaseReceptionService").PurchaseDraftLineDto>().optional(),
  xmlAllocation: z.object({
    subtotal: z.number().nonnegative(), discount: z.number().nonnegative(),
    taxes: z.array(z.object({ purchaseReceptionLineTaxId: z.string().min(1),
      taxableBase: z.number().nonnegative(), taxAmount: z.number().nonnegative() })),
  }).optional(),
  itemId: z.string().nullable().optional(),
  description: z
    .string()
    .min(1, t("purchases.validation.descriptionRequired", "La descripción es obligatoria."))
    .max(300),
  quantity: z.coerce
    .number()
    .positive(t("purchases.validation.quantityPositive", "La cantidad debe ser mayor a 0.")),
  unitPrice: z.coerce
    .number()
    .min(0, t("purchases.validation.unitPriceNonNegative", "El costo no puede ser negativo.")),
  vatCode: z.string().min(1, t("purchases.validation.vatCodeRequired", "Seleccione el código IVA.")),
  discountPct: z.coerce
    .number()
    .min(0)
    .max(100, t("purchases.validation.discountMax", "El descuento no puede superar 100%."))
    .default(0),
  iceCode: z.string().nullable().optional(),
  warehouseId: z.string().nullable().optional(),
  notes: z.string().nullable().optional(),
  purchaseOrderDetailId: z.string().nullable().optional(),
  orderedQuantity: z.number().nullable().optional(),
  packagingLevelId: z.string().nullable().optional(),
  uomCode: z.string().nullable().optional(),
  baseUomCode: z.string().nullable().optional(),
  conversionFactor: z.number().optional(),
  quantityInBaseUom: z.number().optional(),
  // FASE 5 PURCHASE-UX — precio unitario base (antes de descuento/flete) tecleado/conservado por el
  // usuario: autoritativo frente al precio por presentación, que siempre se deriva de él. Solo
  // estado de formulario — no viaja en el payload (ver resolveLinePersistence).
  baseUnitPrice: z.number().optional(),
  // FASE 5 PURCHASE-UX — presentación con la que se interpreta la unidad del documento del
  // proveedor (línea XML), separada de la presentación operativa: una XML editada puede
  // persistirse en unidad base sin perder "2 CAJA = 2 × PACA x12". Solo estado de formulario.
  xmlOriginPackagingLevelId: z.string().nullable().optional(),
  xmlOriginFactor: z.number().optional(),
  // Asignaciones derivadas de las distribuciones de costos guardadas (servidor). Solo lectura para el
  // costo con flete de la línea; nunca viajan en el payload.
  freightAllocated: z.number().optional(),
  otherCostsAllocated: z.number().optional(),
  // Id de la PurchaseReceptionLine de origen — presente solo en líneas que vienen de Recepción
  // Electrónica (recién cargadas con loadFromReception o reabiertas con loadForEdit). Permite que
  // Vincular/Desvincular en /purchases reutilice el mismo backend de Item Matching de Recepción.
  purchaseReceptionLineId: z.string().nullable().optional(),
  // Runtime-only context (not sent to API)
  context: z.custom<PurchaseItemContextDto>().optional(),
  _contextLoading: z.boolean().optional(),
  _readinessIssue: z.enum(["SUPPLIER_CODE_CONFLICT"]).optional(),
  // Detalle XML de solo lectura (Recepción Electrónica → Crear compra) — nunca se envía al API.
  // itemMatchStatus solo viene informado en líneas de Recepción ya conciliadas (Item Matching);
  // las líneas de Compra Manual nunca lo traen.
  itemMatchStatus: z
    .enum(["PENDING", "NEEDS_REVIEW", "AUTO_MATCHED", "MANUALLY_MATCHED"])
    .optional(),
  xmlSupplierCode: z.string().optional(),
  xmlDescription: z.string().optional(),
  xmlSupplierAuxCode: z.string().nullable().optional(),
  // PURCHASE-LINE-PACKAGING-XML-SNAPSHOT-IMMUTABLE-01 — cantidad/precio del comprobante tal como
  // llegó del XML, congelados al cargar el draft. Nunca se sobrescriben por `ctx.updateLine` (esa
  // función solo escribe "quantity"/"unitPrice", los campos editables) — son la única fuente de
  // "Producto recibido (XML)" para Cantidad/Precio, que antes reutilizaba por error los mismos
  // "quantity"/"unitPrice" editables (ver purchaseLinePresentation.ts).
  xmlQuantity: z.number().optional(),
  xmlUnitPrice: z.number().optional(),
  // PURCHASES-HARDENING-P0 (B4-B6) — % descuento original del XML: junto a xmlQuantity/xmlUnitPrice
  // decide si los montos IVA/ICE del XML siguen vigentes (misma regla que backend).
  xmlDiscountPct: z.number().optional(),
  xmlDiscount: z.number().optional(),
  xmlLineSubtotal: z.number().optional(),
  xmlTaxCode: z.string().optional(),
  xmlVatPercentage: z.number().optional(),
  xmlTaxValue: z.number().optional(),
  xmlTotalLine: z.number().optional(),
  xmlTaxableBase: z.number().optional(),
  xmlIceAmount: z.number().optional(),
  xmlIrbpnrAmount: z.number().optional(),
  // PURCHASE-XML-LINE-ADDITIONAL-FIELDS-01 — snapshot de detallesAdicionales/detAdicional, solo
  // lectura ("Datos adicionales XML") — nunca se envía al API, nunca se edita.
  xmlAdditionalFields: z
    .array(z.object({ name: z.string(), value: z.string() }))
    .optional(),
});

export const purchaseLineSchema = createPurchaseLineSchema(passthroughT);

export type PurchaseLineFormValues = z.infer<typeof purchaseLineSchema>;

// ── Main purchase invoice form schema ──────────────────────────────────
export const createPurchaseInvoiceSchema = (t: TFunction) => z.object({
  supplierId: z.string().min(1, t("purchases.validation.supplierRequired", "Seleccione un proveedor.")),
  docTypeCode: z
    .string()
    .min(1, t("purchases.validation.docTypeRequired", "Seleccione el tipo de documento."))
    .default(""),
  invoiceNumber: z.string().min(1, t("purchases.validation.invoiceNumberRequired", "Ingrese el número de factura.")),
  issueDate: z.string().min(1, t("purchases.validation.issueDateRequired", "La fecha de emisión es obligatoria.")),
  accessKey: z.string().optional().default(""),
  authorizationNumber: z.string().optional().default(""),
  authorizationDate: z.string().optional().default(""),
  globalWarehouseId: z.string().optional().default(""),
  dueDate: z.string().optional().default(""),
  notes: z.string().optional().default(""),
  sriPaymentMethodCode: z.string().optional().default(""),
  taxSupportCode: z.string().optional().default(""),
  paymentTermId: z.string().optional().default(""),
  lines: z
    .array(createPurchaseLineSchema(t))
    .min(1, t("purchases.validation.linesRequired", "Agregue al menos una línea.")),
}).superRefine((data, ctx) => {
  if (hasInvalidBreakdown(data.lines)) ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["lines"],
    message: t("purchases.breakdown.incomplete", "El desglose XML debe cuadrar antes de guardar.") });
});

export const purchaseInvoiceSchema = createPurchaseInvoiceSchema(passthroughT);

export type PurchaseInvoiceFormValues = z.infer<typeof purchaseInvoiceSchema>;

// ── Defaults ───────────────────────────────────────────────────────────
export function emptyPurchaseInvoiceForm(): PurchaseInvoiceFormValues {
    return {
    supplierId: "",
    docTypeCode: "",
    invoiceNumber: "",
    issueDate: "",
    accessKey: "",
    authorizationNumber: "",
    authorizationDate: "",
    globalWarehouseId: "",
    dueDate: "",
    notes: "",
    sriPaymentMethodCode: "",
    taxSupportCode: "",
    paymentTermId: "",
    lines: [],
  };
}
