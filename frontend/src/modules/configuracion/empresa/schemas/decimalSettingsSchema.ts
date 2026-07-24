import { z } from 'zod';

const decimalField = z.coerce
  .number()
  .int('Debe ser un número entero.')
  .min(0, 'Mínimo 0 decimales.')
  .max(6, 'Máximo 6 decimales.');

export const decimalSettingsSchema = z.object({
  salesUnitPrice: decimalField,
  purchaseUnitPrice: decimalField,
  quantity: decimalField,
  percentage: decimalField,
  totalAmount: decimalField,
});

export type DecimalSettingsValues = z.infer<typeof decimalSettingsSchema>;

export const defaultDecimalSettingsValues: DecimalSettingsValues = {
  salesUnitPrice: 2,
  purchaseUnitPrice: 4,
  quantity: 4,
  percentage: 2,
  totalAmount: 2,
};
