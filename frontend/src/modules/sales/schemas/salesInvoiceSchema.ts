import { z } from "zod";
import { todayIso } from "../../../lib/formatters/dateFormatters";

// ── Line sub-schema ────────────────────────────────────────────────────
export const salesLineSchema = z.object({
  _key: z.number(),
  itemId: z.string().nullable().optional(),
  warehouseId: z.string().nullable().optional(),
  description: z.string().min(1, "La descripción es obligatoria.").max(300),
  quantity: z.coerce.number().positive("La cantidad debe ser mayor a 0."),
  unitPrice: z.coerce.number().min(0, "El precio no puede ser negativo."),
  vatCode: z
    .string()
    .min(
      1,
      "El producto no tiene código IVA de venta configurado. Verifique el maestro de productos.",
    ),
  discountPct: z.coerce
    .number()
    .min(0)
    .max(100, "El descuento no puede superar 100%.")
    .default(0),
  iceCode: z.string().nullable().optional(),
  notes: z.string().nullable().optional(),
  // ── SALES-PRESENTATIONS-03: venta por presentación ──────────────────
  // packagingLevelId undefined/null = venta en unidad base (comportamiento actual preservado,
  // igual que en el backend — ver SalesLinePackagingResolver). quantity sigue siendo la cantidad
  // ingresada por el usuario en la presentación seleccionada (nunca la cantidad base).
  packagingLevelId: z.string().nullable().optional(),
  uomCode: z.string().optional(),
  baseUomCode: z.string().optional(),
  conversionFactor: z.number().positive().optional(),
  _sku: z.string().optional(),
  _name: z.string().optional(),
  _cost: z.number().optional(),
  _pvp: z.number().optional(),
  _stockQty: z.number().optional(),
  _stockWarehouse: z.string().optional(),
  _tracksStock: z.boolean().optional(),
  /** Presentaciones disponibles del ítem (snapshot tomado al agregar la línea desde el
   * buscador) — solo para poblar el selector, nunca se envía al backend. */
  _packagingLevels: z
    .array(
      z.object({
        id: z.string(),
        name: z.string(),
        uomCode: z.string(),
        baseQuantity: z.number(),
        barcode: z.string().nullable(),
        isBaseUnit: z.boolean(),
        isSaleDefault: z.boolean(),
      }),
    )
    .optional(),
});

export type SalesLineFormValues = z.infer<typeof salesLineSchema>;

// ── Payment detail sub-schemas ─────────────────────────────────────────
const cardDetailSchema = z.object({
  cardBrand: z.string().optional(),
  cardLastFour: z.string().max(4).optional(),
  bankName: z.string().optional(),
  authorizationCode: z.string().optional(),
  lotNumber: z.string().optional(),
});

const transferDetailSchema = z.object({
  bankName: z.string().optional(),
  receiptNumber: z.string().optional(),
  transferDate: z.string().optional(),
});

const chequeDetailSchema = z.object({
  bankName: z.string().optional(),
  chequeNumber: z.string().optional(),
  holderName: z.string().optional(),
  cashDate: z.string().optional(),
});

// ── Payment sub-schema ─────────────────────────────────────────────────
export const salesPaymentSchema = z.object({
  _key: z.number(),
  paymentMethodId: z.string().min(1, "Seleccione un método de pago."),
  amount: z.coerce.number().min(0, "El monto no puede ser negativo."),
  reference: z.string().nullable().optional(),
  cardDetail: cardDetailSchema.nullable().optional(),
  transferDetail: transferDetailSchema.nullable().optional(),
  chequeDetail: chequeDetailSchema.nullable().optional(),
});

export type SalesPaymentFormValues = z.infer<typeof salesPaymentSchema>;

// ── Main invoice form schema ───────────────────────────────────────────
export const salesInvoiceSchema = z
  .object({
    customerId: z.string().min(1, "Seleccione un cliente."),
    issueDate: z.string().min(1, "La fecha de emisión es obligatoria."),
    dueDate: z.string().optional().default(""),
    notes: z.string().optional().default(""),
    paymentTermId: z.string().optional().default(""),
    docTypeCode: z.string().default(""),
    sriPaymentMethodCode: z.string().default(""),
    lines: z.array(salesLineSchema).min(1, "Agregue al menos una línea."),
    payments: z.array(salesPaymentSchema).default([]),
  })
  .superRefine((data, ctx) => {
    data.lines.forEach((line, idx) => {
      if (line._tracksStock && !line.warehouseId) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["lines", idx, "warehouseId"],
          message:
            "La bodega es obligatoria para productos que controlan inventario.",
        });
      }
    });
  });

export type SalesInvoiceFormValues = z.infer<typeof salesInvoiceSchema>;

// ── Defaults ───────────────────────────────────────────────────────────
export function emptySalesInvoiceForm(): SalesInvoiceFormValues {
  return {
    customerId: "",
    issueDate: todayIso(),
    dueDate: "",
    notes: "",
    paymentTermId: "",
    docTypeCode: "",
    sriPaymentMethodCode: "",
    lines: [],
    payments: [],
  };
}
