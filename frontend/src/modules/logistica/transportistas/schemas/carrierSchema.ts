import { z } from "zod";

export const carrierSchema = z.object({
  identificationType: z.enum(["RUC", "CI", "PASSPORT"]),
  identificationNumber: z
    .string()
    .min(1, "carriers.validation.identificationRequired")
    .max(20),
  legalName: z
    .string()
    .min(1, "carriers.validation.legalNameRequired")
    .max(200),
  licensePlate: z
    .string()
    .min(1, "carriers.validation.licensePlateRequired")
    .max(20),
  phone: z.string().max(30).optional().or(z.literal("")),
  email: z
    .string()
    .email("carriers.validation.invalidEmail")
    .max(200)
    .optional()
    .or(z.literal("")),
});

export type CarrierFormValues = z.infer<typeof carrierSchema>;

export const defaultCarrierValues: CarrierFormValues = {
  identificationType: "RUC",
  identificationNumber: "",
  legalName: "",
  licensePlate: "",
  phone: "",
  email: "",
};
