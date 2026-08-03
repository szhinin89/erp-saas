import { z } from "zod";

// ── Crear destino financiero ────────────────────────────────────────────
// Espejo de CreateCompanyFinancialDestinationValidator + guard de dominio
// SC-022 (banco sin BankInstitutionCode/BankAccountIdentifierNormalized, o
// caja sin CashRegisterId) — los 8 campos estructurales son inmutables tras
// la creación (§6.4ter), ningún otro formulario de esta fase los reenvía.

export const createFinancialDestinationSchema = z
  .object({
    code: z
      .string()
      .min(1, "El código es obligatorio.")
      .max(30, "El código no puede superar 30 caracteres."),
    name: z
      .string()
      .min(1, "El nombre es obligatorio.")
      .max(200, "El nombre no puede superar 200 caracteres."),
    destinationTypeCode: z.enum(["BankAccount", "CashRegister"], {
      errorMap: () => ({ message: "El tipo de destino es obligatorio." }),
    }),
    accountingAccountId: z.string().min(1, "La cuenta contable es obligatoria."),
    currencyCode: z
      .string()
      .min(1, "La moneda es obligatoria.")
      .max(3, "La moneda debe tener máximo 3 caracteres."),
    cashRegisterId: z.string().optional().default(""),
    bankInstitutionCode: z.string().optional().default(""),
    bankAccountIdentifierNormalized: z.string().optional().default(""),
  })
  .superRefine((data, ctx) => {
    if (data.destinationTypeCode === "CashRegister" && !data.cashRegisterId.trim()) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["cashRegisterId"],
        message: "Seleccione la caja asociada.",
      });
    }
    if (data.destinationTypeCode === "BankAccount") {
      if (!data.bankInstitutionCode.trim()) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["bankInstitutionCode"],
          message: "La institución bancaria es obligatoria.",
        });
      }
      if (!data.bankAccountIdentifierNormalized.trim()) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["bankAccountIdentifierNormalized"],
          message: "El número de cuenta es obligatorio.",
        });
      }
    }
  });

export type CreateFinancialDestinationFormValues = z.infer<
  typeof createFinancialDestinationSchema
>;

export function emptyCreateFinancialDestinationForm(): CreateFinancialDestinationFormValues {
  return {
    code: "",
    name: "",
    destinationTypeCode: "BankAccount",
    accountingAccountId: "",
    currencyCode: "USD",
    cashRegisterId: "",
    bankInstitutionCode: "",
    bankAccountIdentifierNormalized: "",
  };
}

// ── Editar destino financiero (solo Name/IsActive/AccountingAccountId) ──
// Espejo de UpdateCompanyFinancialDestinationNameValidator +
// ChangeCompanyFinancialDestinationAccountingAccountValidator — los demás
// campos estructurales se muestran de solo lectura, nunca editables.

export const editFinancialDestinationSchema = z.object({
  name: z
    .string()
    .min(1, "El nombre es obligatorio.")
    .max(200, "El nombre no puede superar 200 caracteres."),
  accountingAccountId: z.string().min(1, "La cuenta contable es obligatoria."),
});

export type EditFinancialDestinationFormValues = z.infer<typeof editFinancialDestinationSchema>;
