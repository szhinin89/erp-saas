import { z } from 'zod';

const journalLineSchema = z.object({
  accountId: z.string().min(1, 'Selecciona una cuenta en cada línea.'),
  debitAmount: z.coerce.number().min(0, 'El débito no puede ser negativo.'),
  creditAmount: z.coerce.number().min(0, 'El crédito no puede ser negativo.'),
  currency: z.string().min(1, 'Selecciona la moneda.'),
});

export const journalEntryFormSchema = z
  .object({
    reference: z.string().min(1, 'Ingresa la referencia.'),
    date: z.string().min(1, 'Selecciona la fecha.'),
    description: z.string().min(1, 'Ingresa la descripción.'),
    lines: z.array(journalLineSchema).min(2, 'Debes registrar al menos dos líneas.'),
  })
  .superRefine((data, ctx) => {
    const debit = data.lines.reduce((s, l) => s + (Number(l.debitAmount) || 0), 0);
    const credit = data.lines.reduce((s, l) => s + (Number(l.creditAmount) || 0), 0);
    if (Math.abs(debit - credit) >= 0.001) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Los totales de débito y crédito deben cuadrar.',
        path: ['lines'],
      });
    }
  });

export type JournalEntryFormValues = z.infer<typeof journalEntryFormSchema>;
