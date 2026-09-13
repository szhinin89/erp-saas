import { apiGet, apiPut } from "../../modules/lib/apiEnvelope";

/**
 * COMPANY-PRECISION-POLICY-SSOT-01. Reemplaza `decimal.config.ts` (LEGACY, ver comentario en ese
 * archivo) como fuente de cálculo de precisión operativa de la empresa. `moneyDecimals` /
 * `taxDecimals` / `accountingDecimals` son FIJOS del sistema (FiscalPrecision backend) — nunca
 * editables desde esta pantalla, incluidos solo para que el frontend tenga un único objeto.
 */
export type PrecisionProfileType = "StandardCommercial" | "HighPrecision" | "Custom";

export type PrecisionPolicy = {
  profileType: PrecisionProfileType;
  salesUnitPriceDecimals: number;
  purchaseUnitPriceDecimals: number;
  quantityDecimals: number;
  percentageDecimals: number;
  unitCostDecimals: number;
  averageCostDecimals: number;
  conversionFactorDecimals: number;
  settlementToleranceAmount: number;
  isLocked: boolean;
  lockedAt: string | null;
  lockedReason: string | null;
  // Fijos del sistema — nunca editables aquí.
  moneyDecimals: number;
  taxDecimals: number;
  accountingDecimals: number;
};

export const PRECISION_POLICY_DEFAULTS: PrecisionPolicy = {
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
};

export const HIGH_PRECISION_DEFAULTS: Pick<
  PrecisionPolicy,
  | "salesUnitPriceDecimals"
  | "purchaseUnitPriceDecimals"
  | "quantityDecimals"
  | "percentageDecimals"
  | "unitCostDecimals"
  | "averageCostDecimals"
  | "conversionFactorDecimals"
  | "settlementToleranceAmount"
> = {
  salesUnitPriceDecimals: 4,
  purchaseUnitPriceDecimals: 6,
  quantityDecimals: 6,
  percentageDecimals: 4,
  unitCostDecimals: 6,
  averageCostDecimals: 6,
  conversionFactorDecimals: 8,
  settlementToleranceAmount: 0.01,
};

let _cache: PrecisionPolicy | null = null;

export async function loadPrecisionPolicy(): Promise<PrecisionPolicy> {
  try {
    const cfg = await apiGet<PrecisionPolicy>("/api/v1/config/precision-policy");
    _cache = cfg;
    return cfg;
  } catch {
    _cache = PRECISION_POLICY_DEFAULTS;
    return PRECISION_POLICY_DEFAULTS;
  }
}

export function getPrecisionPolicy(): PrecisionPolicy {
  return _cache ?? PRECISION_POLICY_DEFAULTS;
}

export type UpdatePrecisionPolicyInput = Omit<
  PrecisionPolicy,
  "isLocked" | "lockedAt" | "lockedReason" | "moneyDecimals" | "taxDecimals" | "accountingDecimals"
>;

export async function savePrecisionPolicy(
  input: UpdatePrecisionPolicyInput,
): Promise<PrecisionPolicy> {
  const result = await apiPut<PrecisionPolicy>(
    "/api/v1/config/precision-policy",
    input,
  );
  _cache = result;
  return result;
}
