import type { useI18n } from "../../../../i18n/i18n";

type TFunction = ReturnType<typeof useI18n>["t"];

/**
 * ACCOUNTING-REPORTS-UX-13 — validación local del rango Desde/Hasta antes de consultar un reporte
 * (fechas de negocio ISO `YYYY-MM-DD`, comparables como texto). `null` = rango válido.
 */
export function dateRangeError(t: TFunction, fromDate: string, toDate: string): string | null {
  if (!fromDate || !toDate) return t("accounting.reports.filter.rangeRequired");
  if (fromDate > toDate) return t("accounting.reports.filter.rangeInvalid");
  return null;
}
