import { z } from "zod";

// TREASURY-BANK-ACCOUNTS-01 — espejo de CreateCompanyBankAccountValidator/UpdateCompanyBankAccountValidator.
// Banco/Tipo/Número son inmutables tras la creación (identifican la cuenta real); Update solo
// modifica Alias/Nombre visible y Cuenta contable.

export const bankAccountTypes = ["Checking", "Savings", "Other"] as const;
export type BankAccountTypeCode = (typeof bankAccountTypes)[number];

export const createBankAccountSchema = z.object({
  bankId: z.string().min(1, "El banco es obligatorio."),
  accountType: z.enum(bankAccountTypes, {
    errorMap: () => ({ message: "El tipo de cuenta es obligatorio." }),
  }),
  accountNumber: z
    .string()
    .min(1, "El número de cuenta es obligatorio.")
    .max(50, "El número de cuenta no puede superar 50 caracteres."),
  displayName: z
    .string()
    .min(1, "El alias/nombre visible es obligatorio.")
    .max(200, "El alias/nombre visible no puede superar 200 caracteres."),
  accountingAccountId: z.string().min(1, "La cuenta contable es obligatoria."),
});

export type CreateBankAccountFormValues = z.infer<typeof createBankAccountSchema>;

export function emptyCreateBankAccountForm(): CreateBankAccountFormValues {
  return {
    bankId: "",
    accountType: "Checking",
    accountNumber: "",
    displayName: "",
    accountingAccountId: "",
  };
}

export const editBankAccountSchema = z.object({
  displayName: z
    .string()
    .min(1, "El alias/nombre visible es obligatorio.")
    .max(200, "El alias/nombre visible no puede superar 200 caracteres."),
  accountingAccountId: z.string().min(1, "La cuenta contable es obligatoria."),
});

export type EditBankAccountFormValues = z.infer<typeof editBankAccountSchema>;
