import { apiGet, apiPut } from "../../modules/lib/apiEnvelope";

/**
 * Cliente frontend de la política de precisión de la empresa. El BACKEND es la única fuente de
 * verdad de valores, defaults, rangos, perfiles y lock (CompanyPrecisionPolicy +
 * PrecisionPolicyDefinitions): aquí NO existe ningún valor por defecto ni rango escrito a mano.
 * Si la API falla, se propaga el error — nunca se inventan valores.
 *
 * `moneyDecimals` / `taxDecimals` / `accountingDecimals` son FIJOS del sistema (FiscalPrecision
 * backend) — nunca editables desde la pantalla de precisión.
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

/**
 * ZH-DESIGN-SYSTEM-PRECISION-01B: ÚNICO contrato semántico de precisión del frontend — QUÉ
 * representa un valor. El Design System traduce la semántica a decimales solo vía
 * `resolvePrecisionDecimals`; ningún componente/módulo debe mantener su propio mapeo.
 * `settlementToleranceAmount` queda fuera a propósito: es un monto, no una cantidad de decimales.
 */
export type PrecisionKind =
  | "money"
  | "tax"
  | "accounting"
  | "salesUnitPrice"
  | "purchaseUnitPrice"
  | "unitCost"
  | "averageCost"
  | "quantity"
  | "percentage"
  | "conversionFactor";

/** Campos de la policy que expresan una cantidad de decimales. */
export type PrecisionDecimalsField = Extract<keyof PrecisionPolicy, `${string}Decimals`>;

/**
 * SSOT del mapeo semántica → campo real de la policy. `satisfies Record<…>` obliga en compile-time
 * a mapear toda semántica nueva (y rechaza claves sobrantes). money/tax/accounting se leen de la
 * policy como cualquier otro campo: que hoy compartan escala es un detalle del backend.
 */
export const PRECISION_FIELD_BY_KIND = {
  money: "moneyDecimals",
  tax: "taxDecimals",
  accounting: "accountingDecimals",
  salesUnitPrice: "salesUnitPriceDecimals",
  purchaseUnitPrice: "purchaseUnitPriceDecimals",
  unitCost: "unitCostDecimals",
  averageCost: "averageCostDecimals",
  quantity: "quantityDecimals",
  percentage: "percentageDecimals",
  conversionFactor: "conversionFactorDecimals",
} as const satisfies Readonly<Record<PrecisionKind, PrecisionDecimalsField>>;

/**
 * Decimales que la policy de la empresa define para `kind`. Puro: no lee caché/API/React, no
 * formatea ni redondea, no muta la policy y no aplica defaults — un campo ausente o no entero es
 * un error de contrato (fail-closed), nunca motivo para inventar una precisión.
 */
export function resolvePrecisionDecimals(
  policy: Readonly<Pick<PrecisionPolicy, PrecisionDecimalsField>>,
  kind: PrecisionKind,
): number {
  const field = PRECISION_FIELD_BY_KIND[kind];
  const decimals = policy[field];
  if (!Number.isInteger(decimals)) {
    throw new TypeError(`La política de precisión no define un entero válido para "${kind}" (${field}).`);
  }
  return decimals;
}

/** Metadata estática (GET /precision-policy/metadata): definiciones y perfiles predefinidos. */
export type PrecisionFieldMetadata = {
  key: string;
  kind: "Decimals" | "Amount";
  min: number;
  max: number;
  defaultValue: number;
};

export type PrecisionProfileMetadata = {
  profileType: Exclude<PrecisionProfileType, "Custom">;
  values: Record<string, number>;
};

export type PrecisionPolicyMetadata = {
  fields: PrecisionFieldMetadata[];
  profiles: PrecisionProfileMetadata[];
};

export class PrecisionPolicyNotLoadedError extends Error {
  constructor() {
    super(
      "La configuración de precisión de la empresa no está cargada. No se usan valores por defecto.",
    );
    this.name = "PrecisionPolicyNotLoadedError";
  }
}

let _cache: PrecisionPolicy | null = null;

const BASE = "/api/v1/config/precision-policy";

/** Carga la política de la empresa activa y la deja disponible para `getPrecisionPolicy()`. Lanza si la API falla. */
export async function loadPrecisionPolicy(): Promise<PrecisionPolicy> {
  const cfg = await apiGet<PrecisionPolicy>(BASE);
  _cache = cfg;
  return cfg;
}

export function loadPrecisionPolicyMetadata(): Promise<PrecisionPolicyMetadata> {
  return apiGet<PrecisionPolicyMetadata>(`${BASE}/metadata`);
}

export function isPrecisionPolicyLoaded(): boolean {
  return _cache !== null;
}

/** Olvida la política cacheada (logout / cambio de empresa) — evita usar la de otra empresa. */
export function clearPrecisionPolicy(): void {
  _cache = null;
}

/** Política cargada de la empresa activa. Lanza `PrecisionPolicyNotLoadedError` si aún no se cargó. */
export function getPrecisionPolicy(): PrecisionPolicy {
  if (!_cache) throw new PrecisionPolicyNotLoadedError();
  return _cache;
}

/** Solo tests: fija la política cacheada sin pasar por la API. */
export function setPrecisionPolicyForTests(policy: PrecisionPolicy | null): void {
  _cache = policy;
}

export type UpdatePrecisionPolicyInput = Omit<
  PrecisionPolicy,
  "isLocked" | "lockedAt" | "lockedReason" | "moneyDecimals" | "taxDecimals" | "accountingDecimals"
>;

export async function savePrecisionPolicy(
  input: UpdatePrecisionPolicyInput,
): Promise<PrecisionPolicy> {
  const result = await apiPut<PrecisionPolicy>(BASE, input);
  _cache = result;
  return result;
}
