import { z } from "zod";
import {
  LocationTypeEnum,
  LocationPurposeEnum,
} from "../types/businessPartner.types";

export const locationSchema = z.object({
  name: z.string().min(1, "Nombre obligatorio.").max(150),
  type: z.number({ required_error: "Seleccione un tipo." }),
  purpose: z.number().min(0).default(0),
  addressLine: z.string().min(5, "Mínimo 5 caracteres.").max(300),
  provinceCode: z.string().max(2).optional().or(z.literal("")),
  cantonCode: z.string().max(4).optional().or(z.literal("")),
  parishCode: z.string().max(6).optional().or(z.literal("")),
  phone: z.string().max(20).optional().or(z.literal("")),
  email: z.string().max(254).optional().or(z.literal("")),
  isPrimary: z.boolean().default(false),
  otherDescription: z.string().max(100).optional().or(z.literal("")),
});
export type LocationFormValues = z.infer<typeof locationSchema>;

export const LOCATION_TYPES = [
  { value: LocationTypeEnum.Matrix, label: "Matriz" },
  { value: LocationTypeEnum.Branch, label: "Sucursal" },
  { value: LocationTypeEnum.Office, label: "Oficina" },
  { value: LocationTypeEnum.Warehouse, label: "Bodega" },
  { value: LocationTypeEnum.DeliveryPoint, label: "Punto de entrega" },
  { value: LocationTypeEnum.Other, label: "Otro" },
];

export const PURPOSE_FLAGS = [
  { flag: LocationPurposeEnum.Billing, label: "Facturación" },
  { flag: LocationPurposeEnum.Delivery, label: "Entrega" },
  { flag: LocationPurposeEnum.Fiscal, label: "Fiscal" },
  { flag: LocationPurposeEnum.Correspondence, label: "Correspondencia" },
];
