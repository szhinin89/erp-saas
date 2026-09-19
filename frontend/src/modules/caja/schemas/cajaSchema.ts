import { z } from "zod";

type Translate = (key: string, params?: Record<string, string | number>) => string;

// ── Open session schema ────────────────────────────────────────────────
// El punto de emisión ya no se elige manualmente (ADR — Rediseño del módulo de Caja): el usuario
// selecciona una caja (CashRegister) de la sucursal activa; el servidor resuelve el punto de
// emisión desde el CashRegister elegido.
export const openCashSessionSchema = (t: Translate) => z.object({
  cashRegisterId: z.string().min(1, t("caja.validation.register")),
  openingAmount: z.coerce
    .number({ invalid_type_error: t("caja.validation.number") })
    .min(0, t("caja.validation.openingAmount")),
  notes: z.string().max(500, t("caja.validation.maxLength", { max: 500 })).optional().default(""),
});

export type OpenCashSessionFormValues = z.infer<ReturnType<typeof openCashSessionSchema>>;

export function emptyOpenForm(): OpenCashSessionFormValues {
  return { cashRegisterId: "", openingAmount: 0, notes: "" };
}

// ── Record movement schema ─────────────────────────────────────────────
// TREASURY-CASH-MANUAL-MOVEMENTS-01 — reasonId obligatorio: el motivo viene del catálogo dinámico
// filtrado por Tipo (ver cajaService.getCashMovementReasons), nunca texto libre ni hardcodeado.
export const recordMovementSchema = (t: Translate) => z.object({
  movementType: z.string().min(1, t("caja.validation.movementType")),
  reasonId: z.string().min(1, t("caja.validation.reason")),
  amount: z.coerce.number({ invalid_type_error: t("caja.validation.number") }).positive(t("caja.validation.amount")),
  description: z.string().min(1, t("caja.validation.description")).max(300, t("caja.validation.maxLength", { max: 300 })),
});

export type RecordMovementFormValues = z.infer<ReturnType<typeof recordMovementSchema>>;

export function emptyMovementForm(): RecordMovementFormValues {
  return { movementType: "", reasonId: "", amount: 0, description: "" };
}

// ── Closing count schema ───────────────────────────────────────────────
export const closingCountSchema = (t: Translate) => z.object({
  _key: z.number({ invalid_type_error: t("caja.validation.number") }),
  denominationValue: z.coerce
    .number({ invalid_type_error: t("caja.validation.number") })
    .positive(t("caja.validation.value")),
  denominationLabel: z.string().min(1, t("caja.validation.label")).max(30, t("caja.validation.maxLength", { max: 30 })),
  quantity: z.coerce
    .number({ invalid_type_error: t("caja.validation.number") })
    .int(t("caja.validation.integer"))
    .min(0, t("caja.validation.quantity")),
});

export type ClosingCountFormValues = z.infer<ReturnType<typeof closingCountSchema>>;

export const closeCashSessionSchema = (t: Translate) => z.object({
  closingCounts: z
    .array(closingCountSchema(t))
    .min(1, t("caja.validation.denomination")),
  closeNotes: z.string().max(500, t("caja.validation.maxLength", { max: 500 })).optional().default(""),
});

export type CloseCashSessionFormValues = z.infer<ReturnType<typeof closeCashSessionSchema>>;

// ── USD denominations ──────────────────────────────────────────────────
export const USD_DENOMINATIONS: { value: number; label: string }[] = [
  { value: 100, label: "$100" },
  { value: 50, label: "$50" },
  { value: 20, label: "$20" },
  { value: 10, label: "$10" },
  { value: 5, label: "$5" },
  { value: 1, label: "$1" },
  { value: 0.5, label: "$0.50" },
  { value: 0.25, label: "$0.25" },
  { value: 0.1, label: "$0.10" },
  { value: 0.05, label: "$0.05" },
  { value: 0.01, label: "$0.01" },
];

export function defaultClosingCounts(): ClosingCountFormValues[] {
  return USD_DENOMINATIONS.map((d, i) => ({
    _key: i,
    denominationValue: d.value,
    denominationLabel: d.label,
    quantity: 0,
  }));
}
