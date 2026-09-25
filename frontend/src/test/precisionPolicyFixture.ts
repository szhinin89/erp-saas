import type { PrecisionPolicy, PrecisionPolicyMetadata } from "../lib/config/precisionPolicy.config";

/**
 * SOLO TESTS. Política de precisión de ejemplo para que los tests unitarios/de componentes no
 * dependan de la API. No es una fuente de verdad ni un default de la app: en producción no existe
 * ningún valor por defecto (la política sale siempre del backend).
 */
export const TEST_PRECISION_POLICY: PrecisionPolicy = {
  profileType: "StandardCommercial",
  salesUnitPriceDecimals: 2,
  purchaseUnitPriceDecimals: 4,
  quantityDecimals: 4,
  percentageDecimals: 2,
  unitCostDecimals: 6,
  averageCostDecimals: 6,
  conversionFactorDecimals: 6,
  settlementToleranceAmount: 0.01,
  isLocked: false,
  lockedAt: null,
  lockedReason: null,
  moneyDecimals: 2,
  taxDecimals: 2,
  accountingDecimals: 2,
  fiscalPercentageDecimals: 2,
};

/**
 * SOLO TESTS. Metadata de ejemplo con la forma de `GET /precision-policy/metadata`. Espejo de
 * prueba: la definición real vive únicamente en el backend (PrecisionPolicyDefinitions).
 */
export const TEST_PRECISION_METADATA: PrecisionPolicyMetadata = {
  fields: [
    { key: "salesUnitPriceDecimals", kind: "Decimals", min: 2, max: 6, defaultValue: 2 },
    { key: "purchaseUnitPriceDecimals", kind: "Decimals", min: 2, max: 10, defaultValue: 4 },
    { key: "quantityDecimals", kind: "Decimals", min: 0, max: 6, defaultValue: 4 },
    { key: "percentageDecimals", kind: "Decimals", min: 2, max: 6, defaultValue: 2 },
    { key: "unitCostDecimals", kind: "Decimals", min: 2, max: 10, defaultValue: 6 },
    { key: "averageCostDecimals", kind: "Decimals", min: 2, max: 10, defaultValue: 6 },
    { key: "conversionFactorDecimals", kind: "Decimals", min: 2, max: 10, defaultValue: 6 },
    { key: "settlementToleranceAmount", kind: "Amount", min: 0, max: 0.02, defaultValue: 0.01 },
  ],
  profiles: [
    {
      profileType: "StandardCommercial",
      values: {
        salesUnitPriceDecimals: 2,
        purchaseUnitPriceDecimals: 4,
        quantityDecimals: 4,
        percentageDecimals: 2,
        unitCostDecimals: 6,
        averageCostDecimals: 6,
        conversionFactorDecimals: 6,
        settlementToleranceAmount: 0.01,
      },
    },
    {
      profileType: "HighPrecision",
      values: {
        salesUnitPriceDecimals: 4,
        purchaseUnitPriceDecimals: 6,
        quantityDecimals: 6,
        percentageDecimals: 4,
        unitCostDecimals: 6,
        averageCostDecimals: 6,
        conversionFactorDecimals: 8,
        settlementToleranceAmount: 0.01,
      },
    },
  ],
};
