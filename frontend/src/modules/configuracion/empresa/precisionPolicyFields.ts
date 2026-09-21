import type { PrecisionPolicyFormValues } from "./schemas/precisionPolicySchema";

/**
 * ERP-PRECISION-POLICY-SETTINGS-UX-02: estructura de la pantalla de precisión por empresa.
 * Solo agrupa y describe las keys EXISTENTES de CompanyPrecisionPolicy — no cambia rangos,
 * defaults ni cálculos. Todos estos campos son decimales de valores UNITARIOS (o factor/porcentaje),
 * nunca de subtotales, impuestos ni totales (esos son "Precisión fiscal", no configurable).
 */

export type PrecisionDecimalField = Exclude<
  keyof PrecisionPolicyFormValues,
  "profileType" | "settlementToleranceAmount"
>;

export type PrecisionExampleKind = "price" | "quantity" | "percentage" | "factor";

export interface PrecisionFieldDef {
  name: PrecisionDecimalField;
  /** Sufijo de las claves i18n: settings.company.precision.field.<i18nKey>.label|desc */
  i18nKey: string;
  example: PrecisionExampleKind;
}

export interface PrecisionSectionDef {
  id: "prices" | "costs" | "quantities" | "other";
  icon: string;
  fields: PrecisionFieldDef[];
}

export const PRECISION_SECTIONS: PrecisionSectionDef[] = [
  {
    id: "prices",
    icon: "sell",
    fields: [
      { name: "salesUnitPriceDecimals", i18nKey: "salesUnitPrice", example: "price" },
      { name: "purchaseUnitPriceDecimals", i18nKey: "purchaseUnitPrice", example: "price" },
    ],
  },
  {
    id: "costs",
    icon: "payments",
    fields: [
      { name: "unitCostDecimals", i18nKey: "unitCost", example: "price" },
      { name: "averageCostDecimals", i18nKey: "averageCost", example: "price" },
    ],
  },
  {
    id: "quantities",
    icon: "straighten",
    fields: [
      { name: "quantityDecimals", i18nKey: "quantity", example: "quantity" },
      { name: "conversionFactorDecimals", i18nKey: "conversionFactor", example: "factor" },
    ],
  },
  {
    id: "other",
    icon: "percent",
    fields: [{ name: "percentageDecimals", i18nKey: "percentage", example: "percentage" }],
  },
];

const DIGITS = "1234567890123456789";

/**
 * Ejemplo con exactamente `decimals` decimales, truncando dígitos distintos (sin aritmética de
 * punto flotante ni redondeo) para que se vea claramente cuántos decimales implica el valor.
 */
export function precisionExample(kind: PrecisionExampleKind, decimals: number): string {
  const n = Number.isFinite(decimals) ? Math.max(0, Math.min(Math.trunc(decimals), DIGITS.length)) : 0;
  const frac = n > 0 ? `.${DIGITS.slice(0, n)}` : "";
  switch (kind) {
    case "price":
      return `$0${frac}`;
    case "quantity":
      return `12${frac}`;
    case "factor":
      return `1${frac}`;
    case "percentage":
      return `15${frac}%`;
  }
}
