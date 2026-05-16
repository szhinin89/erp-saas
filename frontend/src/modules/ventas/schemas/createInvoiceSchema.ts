import { z } from 'zod';

export const invoiceLineSchema = z.object({
  localId:        z.string(),
  productId:      z.string().optional(),
  sku:            z.string().optional(),
  description:    z.string().min(1, 'La descripción es requerida'),
  quantity:       z.coerce.number().min(0.01, 'Cantidad mínima 0.01'),
  unitPrice:      z.coerce.number().min(0, 'El precio no puede ser negativo'),
  discountAmount: z.coerce.number().min(0).default(0),
  vatRate:        z.coerce.number().min(0).max(1).default(0.15), // 15% IVA Ecuador
});

export const createInvoiceSchema = z.object({
  customerId:  z.string().min(1, 'Seleccione un cliente'),
  warehouseId: z.string().min(1, 'Seleccione una bodega'),
  issueDate:   z.string().min(1, 'La fecha de emisión es requerida'),
  currency:    z.string().default('USD'),
  notes:       z.string().optional(),
  items:       z.array(invoiceLineSchema).min(1, 'Agregue al menos un ítem'),
});

export type InvoiceLineValues  = z.infer<typeof invoiceLineSchema>;
export type CreateInvoiceValues = z.infer<typeof createInvoiceSchema>;

export function calcLineAmounts(line: Pick<InvoiceLineValues, 'quantity' | 'unitPrice' | 'discountAmount' | 'vatRate'>) {
  const subtotal  = line.quantity * line.unitPrice;
  const base      = subtotal - (line.discountAmount ?? 0);
  const vatAmount = base * (line.vatRate ?? 0.15);
  const total     = base + vatAmount;
  return { subtotal, vatAmount, total };
}

export function calcInvoiceTotals(lines: InvoiceLineValues[]) {
  return lines.reduce(
    (acc, l) => {
      const { subtotal, vatAmount, total } = calcLineAmounts(l);
      return {
        subtotal:  acc.subtotal  + subtotal,
        discount:  acc.discount  + (l.discountAmount ?? 0),
        vat:       acc.vat       + vatAmount,
        total:     acc.total     + total,
      };
    },
    { subtotal: 0, discount: 0, vat: 0, total: 0 }
  );
}

let _idCounter = 0;
export function newInvoiceLineId() { return `line-${++_idCounter}`; }

export function emptyInvoiceLine(): InvoiceLineValues {
  return {
    localId:        newInvoiceLineId(),
    productId:      '',
    sku:            '',
    description:    '',
    quantity:       1,
    unitPrice:      0,
    discountAmount: 0,
    vatRate:        0.15,
  };
}
