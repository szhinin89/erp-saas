import { z } from "zod";

export const cashRegistersPageSchema = z.object({
  branchId: z.string().min(1, "Selecciona una sucursal."),
  code: z
    .string()
    .min(1, "Ingresa el código.")
    .max(30, "Máximo 30 caracteres."),
  name: z
    .string()
    .min(1, "Ingresa el nombre.")
    .max(100, "Máximo 100 caracteres."),
  emissionPointId: z.string().min(1, "Selecciona un punto de emisión."),
  notes: z
    .string()
    .max(500, "Máximo 500 caracteres.")
    .optional()
    .or(z.literal("")),
  defaultWarehouseId: z.string().optional().or(z.literal("")),
  defaultCustomerId: z.string().optional().or(z.literal("")),
});

export type CashRegistersPageFormValues = z.infer<
  typeof cashRegistersPageSchema
>;

export function emptyCashRegistersPageForm(): CashRegistersPageFormValues {
  return {
    branchId: "",
    code: "",
    name: "",
    emissionPointId: "",
    notes: "",
    defaultWarehouseId: "",
    defaultCustomerId: "",
  };
}
