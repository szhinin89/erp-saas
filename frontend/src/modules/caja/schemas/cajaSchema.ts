import { z } from 'zod';

// ── Open session schema ────────────────────────────────────────────────
// El punto de emisión ya no se elige manualmente (ADR — Rediseño del módulo de Caja): el usuario
// selecciona una caja (CashRegister) de la sucursal activa; el servidor resuelve el punto de
// emisión desde el CashRegister elegido.
export const openCashSessionSchema = z.object({
  cashRegisterId: z.string().min(1, 'Seleccione una caja.'),
  openingAmount: z.coerce.number().min(0, 'El monto de apertura no puede ser negativo.'),
  notes: z.string().max(500).optional().default(''),
});

export type OpenCashSessionFormValues = z.infer<typeof openCashSessionSchema>;

export function emptyOpenForm(): OpenCashSessionFormValues {
  return { cashRegisterId: '', openingAmount: 0, notes: '' };
}

// ── Record movement schema ─────────────────────────────────────────────
export const recordMovementSchema = z.object({
  movementType: z.string().min(1, 'Seleccione el tipo de movimiento.'),
  amount: z.coerce.number().positive('El monto debe ser mayor a cero.'),
  description: z.string().min(1, 'La descripción es obligatoria.').max(300),
});

export type RecordMovementFormValues = z.infer<typeof recordMovementSchema>;

export function emptyMovementForm(): RecordMovementFormValues {
  return { movementType: '', amount: 0, description: '' };
}

// ── Closing count schema ───────────────────────────────────────────────
export const closingCountSchema = z.object({
  _key: z.number(),
  denominationValue: z.coerce.number().positive('El valor debe ser mayor a cero.'),
  denominationLabel: z.string().min(1, 'La etiqueta es obligatoria.').max(30),
  quantity: z.coerce.number().int().min(0, 'La cantidad no puede ser negativa.'),
});

export type ClosingCountFormValues = z.infer<typeof closingCountSchema>;

export const closeCashSessionSchema = z.object({
  closingCounts: z.array(closingCountSchema).min(1, 'Agregue al menos una denominación.'),
  closeNotes: z.string().max(500).optional().default(''),
});

export type CloseCashSessionFormValues = z.infer<typeof closeCashSessionSchema>;

// ── USD denominations ──────────────────────────────────────────────────
export const USD_DENOMINATIONS: { value: number; label: string }[] = [
  { value: 100, label: '$100' },
  { value: 50, label: '$50' },
  { value: 20, label: '$20' },
  { value: 10, label: '$10' },
  { value: 5, label: '$5' },
  { value: 1, label: '$1' },
  { value: 0.50, label: '$0.50' },
  { value: 0.25, label: '$0.25' },
  { value: 0.10, label: '$0.10' },
  { value: 0.05, label: '$0.05' },
  { value: 0.01, label: '$0.01' },
];

export function defaultClosingCounts(): ClosingCountFormValues[] {
  return USD_DENOMINATIONS.map((d, i) => ({
    _key: i,
    denominationValue: d.value,
    denominationLabel: d.label,
    quantity: 0,
  }));
}
