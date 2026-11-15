import { z } from "zod";
import {
  LocationTypeEnum,
  LocationPurposeEnum,
} from "../types/businessPartner.types";

type Translate = (key: string, fallback?: string) => string;

/** Esquema del formulario de ubicación; los mensajes se resuelven con `t` (idioma activo). */
export const locationSchema = (t: Translate) =>
  z.object({
    name: z.string().min(1, t("masterdata.bp.detail.loc.validation.nameRequired", "Nombre obligatorio.")).max(150),
    type: z.number({ required_error: t("masterdata.bp.detail.loc.validation.typeRequired", "Seleccione un tipo.") }),
    purpose: z.number().min(0).default(0),
    addressLine: z.string().min(5, t("masterdata.bp.detail.loc.validation.addressMin", "Mínimo 5 caracteres.")).max(300),
    provinceCode: z.string().max(2).optional().or(z.literal("")),
    cantonCode: z.string().max(4).optional().or(z.literal("")),
    parishCode: z.string().max(6).optional().or(z.literal("")),
    phone: z.string().max(20).optional().or(z.literal("")),
    email: z.string().max(254).optional().or(z.literal("")),
    isPrimary: z.boolean().default(false),
    otherDescription: z.string().max(100).optional().or(z.literal("")),
  });
export type LocationFormValues = z.infer<ReturnType<typeof locationSchema>>;

const T = "masterdata.bp.detail.loc.";
export const LOCATION_TYPES = [
  { value: LocationTypeEnum.Matrix, labelKey: T + "type.matrix", fallback: "Matriz" },
  { value: LocationTypeEnum.Branch, labelKey: T + "type.branch", fallback: "Sucursal" },
  { value: LocationTypeEnum.Office, labelKey: T + "type.office", fallback: "Oficina" },
  { value: LocationTypeEnum.Warehouse, labelKey: T + "type.warehouse", fallback: "Bodega" },
  { value: LocationTypeEnum.DeliveryPoint, labelKey: T + "type.deliveryPoint", fallback: "Punto de entrega" },
  { value: LocationTypeEnum.Other, labelKey: T + "type.other", fallback: "Otro" },
];

export const PURPOSE_FLAGS = [
  { flag: LocationPurposeEnum.Billing, labelKey: T + "purpose.billing", fallback: "Facturación" },
  { flag: LocationPurposeEnum.Delivery, labelKey: T + "purpose.delivery", fallback: "Entrega" },
  { flag: LocationPurposeEnum.Fiscal, labelKey: T + "purpose.fiscal", fallback: "Fiscal" },
  { flag: LocationPurposeEnum.Correspondence, labelKey: T + "purpose.correspondence", fallback: "Correspondencia" },
];
