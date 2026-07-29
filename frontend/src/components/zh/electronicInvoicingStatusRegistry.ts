import type { ZHFormAlertType } from "./ZHForm";
import type { ElectronicInvoicingStatus } from "../../modules/configuracion/facturacionElectronica/api/electronicInvoicingService";

export type ElectronicInvoicingStatusVisual = {
  variant: ZHFormAlertType;
  icon: string;
  /** Clave i18n del título/mensaje principal. */
  messageKey: string;
  /** Clave i18n del texto secundario. */
  detailKey: string;
};

/**
 * Único lugar del ERP que traduce `ElectronicInvoicingStatus` (calculado por el backend) a su
 * representación visual (color/icono/textos). `ZHElectronicEnvironmentBanner` y cualquier otra
 * UI que necesite mostrar este estado deben leer de aquí — nunca recalcular el estado a partir
 * de combinaciones de booleanos.
 *
 * Extensibilidad: un estado nuevo del backend se incorpora agregando una entrada aquí — ninguna
 * pantalla consumidora (Ventas, Notas de Crédito, Retenciones, Guías, etc.) requiere cambios.
 */
export const ELECTRONIC_INVOICING_STATUS_REGISTRY: Record<
  ElectronicInvoicingStatus,
  ElectronicInvoicingStatusVisual
> = {
  Ready: {
    variant: "success",
    icon: "verified",
    messageKey: "common.electronicEnvironmentBanner.ready.message",
    detailKey: "common.electronicEnvironmentBanner.ready.detail",
  },
  Testing: {
    variant: "warning",
    icon: "science",
    messageKey: "common.electronicEnvironmentBanner.testing.message",
    detailKey: "common.electronicEnvironmentBanner.testing.detail",
  },
  Incomplete: {
    variant: "attention",
    icon: "error",
    messageKey: "common.electronicEnvironmentBanner.incomplete.message",
    detailKey: "common.electronicEnvironmentBanner.incomplete.detail",
  },
  NotConfigured: {
    variant: "error",
    icon: "cancel",
    messageKey: "common.electronicEnvironmentBanner.notConfigured.message",
    detailKey: "common.electronicEnvironmentBanner.notConfigured.detail",
  },
  CertificateExpired: {
    variant: "error",
    icon: "event_busy",
    messageKey: "common.electronicEnvironmentBanner.certificateExpired.message",
    detailKey: "common.electronicEnvironmentBanner.certificateExpired.detail",
  },
  CertificateExpiring: {
    variant: "warning",
    icon: "hourglass_top",
    messageKey:
      "common.electronicEnvironmentBanner.certificateExpiring.message",
    detailKey: "common.electronicEnvironmentBanner.certificateExpiring.detail",
  },
  SriUnavailable: {
    variant: "attention",
    icon: "cloud_off",
    messageKey: "common.electronicEnvironmentBanner.sriUnavailable.message",
    detailKey: "common.electronicEnvironmentBanner.sriUnavailable.detail",
  },
  Disabled: {
    variant: "neutral",
    icon: "block",
    messageKey: "common.electronicEnvironmentBanner.disabled.message",
    detailKey: "common.electronicEnvironmentBanner.disabled.detail",
  },
  Error: {
    variant: "error",
    icon: "help",
    messageKey: "common.electronicEnvironmentBanner.error.message",
    detailKey: "common.electronicEnvironmentBanner.error.detail",
  },
};
