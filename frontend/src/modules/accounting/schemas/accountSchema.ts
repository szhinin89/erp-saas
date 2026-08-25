import { z } from "zod";

// ── Crear cuenta ────────────────────────────────────────────────────────
// Espejo de CreateAccountCommandValidator (ERP.Application). Code/AccountType/Nature son
// inmutables tras crear (ver UpdateAccountCommand — no los acepta), mismo criterio ya usado en
// CompanyFinancialDestination §6.4ter para campos estructurales.

export const ACCOUNT_TYPE_OPTIONS = [
  { value: "Asset", label: "Activo" },
  { value: "Liability", label: "Pasivo" },
  { value: "Equity", label: "Patrimonio" },
  { value: "Income", label: "Ingreso" },
  { value: "Cost", label: "Costo" },
  { value: "Expense", label: "Gasto" },
] as const;

export const ACCOUNT_NATURE_OPTIONS = [
  { value: "Debit", label: "Débito" },
  { value: "Credit", label: "Crédito" },
] as const;

export const createAccountSchema = z.object({
  code: z
    .string()
    .min(1, "El código es obligatorio.")
    .max(30, "El código no puede superar 30 caracteres."),
  name: z
    .string()
    .min(1, "El nombre es obligatorio.")
    .max(150, "El nombre no puede superar 150 caracteres."),
  parentAccountId: z.string().optional().default(""),
  accountType: z.enum(["Asset", "Liability", "Equity", "Income", "Cost", "Expense"], {
    errorMap: () => ({ message: "El tipo de cuenta es obligatorio." }),
  }),
  nature: z.enum(["Debit", "Credit"], {
    errorMap: () => ({ message: "La naturaleza es obligatoria." }),
  }),
  allowsPosting: z.boolean().default(true),
});

export type CreateAccountFormValues = z.infer<typeof createAccountSchema>;

export function emptyCreateAccountForm(): CreateAccountFormValues {
  return {
    code: "",
    name: "",
    parentAccountId: "",
    accountType: "Asset",
    nature: "Debit",
    allowsPosting: true,
  };
}

// ── Editar cuenta (Name/ParentAccountId/AllowsPosting — Code/Type/Nature no editables) ──
// Espejo de UpdateAccountCommandValidator.

export const editAccountSchema = z.object({
  name: z
    .string()
    .min(1, "El nombre es obligatorio.")
    .max(150, "El nombre no puede superar 150 caracteres."),
  parentAccountId: z.string().optional().default(""),
  allowsPosting: z.boolean().default(true),
});

export type EditAccountFormValues = z.infer<typeof editAccountSchema>;
