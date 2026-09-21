import { z } from "zod";
import type {
  PrecisionPolicyMetadata,
  PrecisionProfileType,
} from "../../../../lib/config/precisionPolicy.config";

/**
 * Valores del formulario de precisión. Los RANGOS y DEFAULTS no viven aquí: los entrega el
 * backend (`GET /precision-policy/metadata`, fuente única PrecisionPolicyDefinitions) y el schema
 * se construye en runtime con `buildPrecisionPolicySchema(metadata)`.
 */
export type PrecisionPolicyFormValues = {
  profileType: PrecisionProfileType;
  salesUnitPriceDecimals: number;
  purchaseUnitPriceDecimals: number;
  quantityDecimals: number;
  percentageDecimals: number;
  unitCostDecimals: number;
  averageCostDecimals: number;
  conversionFactorDecimals: number;
  settlementToleranceAmount: number;
};

export const PRECISION_VALUE_KEYS = [
  "salesUnitPriceDecimals",
  "purchaseUnitPriceDecimals",
  "quantityDecimals",
  "percentageDecimals",
  "unitCostDecimals",
  "averageCostDecimals",
  "conversionFactorDecimals",
  "settlementToleranceAmount",
] as const satisfies readonly (keyof PrecisionPolicyFormValues)[];

export type PrecisionValueKey = (typeof PRECISION_VALUE_KEYS)[number];

/** Falla si la metadata no describe alguna key esperada — nunca se completa con valores propios. */
export function assertPrecisionMetadata(metadata: PrecisionPolicyMetadata): void {
  for (const key of PRECISION_VALUE_KEYS) {
    if (!metadata.fields.some((f) => f.key === key)) {
      throw new Error(`Metadata de precisión incompleta: falta '${key}'.`);
    }
  }
}

export function buildPrecisionPolicySchema(metadata: PrecisionPolicyMetadata) {
  const range = (key: PrecisionValueKey) => {
    const f = metadata.fields.find((x) => x.key === key);
    if (!f) throw new Error(`Metadata de precisión incompleta: falta '${key}'.`);
    return f;
  };
  const decimals = (key: PrecisionValueKey) => {
    const { min, max } = range(key);
    return z.coerce.number().int().min(min).max(max);
  };
  const amount = (key: PrecisionValueKey) => {
    const { min, max } = range(key);
    return z.coerce.number().min(min).max(max);
  };

  return z.object({
    profileType: z.enum(["StandardCommercial", "HighPrecision", "Custom"]),
    salesUnitPriceDecimals: decimals("salesUnitPriceDecimals"),
    purchaseUnitPriceDecimals: decimals("purchaseUnitPriceDecimals"),
    quantityDecimals: decimals("quantityDecimals"),
    percentageDecimals: decimals("percentageDecimals"),
    unitCostDecimals: decimals("unitCostDecimals"),
    averageCostDecimals: decimals("averageCostDecimals"),
    conversionFactorDecimals: decimals("conversionFactorDecimals"),
    settlementToleranceAmount: amount("settlementToleranceAmount"),
  });
}
