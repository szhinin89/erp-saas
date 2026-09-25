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
  /** 04C1 — escala FIJA de porcentajes fiscales (FiscalPrecision.Percentage), p. ej. % de retención SRI. */
  fiscalPercentageDecimals: number;
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
  | "fiscalPercentage"
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
  fiscalPercentage: "fiscalPercentageDecimals",
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

/**
 * ZH-DESIGN-SYSTEM-PRECISION-01C: snapshot inmutable del ÚNICO estado de precisión del frontend.
 * `getPrecisionPolicy()`, `isPrecisionPolicyLoaded()` y los suscriptores leen exactamente esta
 * referencia; se reemplaza solo cuando el estado cambia (referencia estable para
 * `useSyncExternalStore`). Sin política cargada no existe ningún valor por defecto.
 */
export type PrecisionPolicySnapshot =
  | { readonly loaded: false; readonly policy: null }
  | { readonly loaded: true; readonly policy: PrecisionPolicy };

const EMPTY_SNAPSHOT: PrecisionPolicySnapshot = Object.freeze({ loaded: false, policy: null });

/** Única fuente autoritativa en memoria de la PrecisionPolicy activa. */
let _snapshot: PrecisionPolicySnapshot = EMPTY_SNAPSHOT;

/**
 * Control de concurrencia (no es estado de negocio): cada load/save/clear/set toma una generación
 * nueva y una respuesta solo se publica si su generación sigue siendo la vigente. Así una request
 * vieja (otra empresa, un load anterior a un save o a un clear) nunca sobrescribe el estado, sin
 * depender del orden en que resuelvan las promesas ni de poder abortarlas.
 */
let _generation = 0;

/**
 * Generación del save en vuelo mientras siga siendo la operación vigente (solo metadata técnica,
 * nunca una policy). Un load que empieza en ese intervalo pudo leer el backend ANTES de que el save
 * confirmara: no desplaza al save ni se publica. Un clear posterior avanza `_generation` y deja
 * este marcador sin efecto, así los loads de la nueva empresa se publican con normalidad.
 */
let _pendingSaveGeneration: number | null = null;

const _listeners = new Set<() => void>();

function publish(next: PrecisionPolicySnapshot): void {
  if (next === _snapshot) return;
  _snapshot = next;
  for (const listener of [..._listeners]) listener();
}

function publishPolicy(policy: PrecisionPolicy | null): void {
  publish(policy ? Object.freeze({ loaded: true, policy }) : EMPTY_SNAPSHOT);
}

/** Snapshot actual (misma referencia mientras el estado no cambie). */
export function getPrecisionPolicySnapshot(): PrecisionPolicySnapshot {
  return _snapshot;
}

/** Notifica cada cambio del estado aceptado (load/save vigente, clear). Devuelve el unsubscribe. */
export function subscribePrecisionPolicy(listener: () => void): () => void {
  _listeners.add(listener);
  return () => {
    _listeners.delete(listener);
  };
}

const BASE = "/api/v1/config/precision-policy";

/**
 * Carga la política de la empresa activa y la deja disponible para `getPrecisionPolicy()`. Lanza
 * si la API falla (sin tocar el estado vigente). Devuelve siempre la respuesta del backend, pero
 * solo la publica si ningún load/save/clear posterior la dejó obsoleta. Iniciado durante un save
 * vigente es stale desde el origen: devuelve la respuesta pero nunca la publica.
 */
export async function loadPrecisionPolicy(): Promise<PrecisionPolicy> {
  if (_pendingSaveGeneration === _generation) return apiGet<PrecisionPolicy>(BASE);
  const generation = ++_generation;
  const cfg = await apiGet<PrecisionPolicy>(BASE);
  if (generation === _generation) publishPolicy(cfg);
  return cfg;
}

export function loadPrecisionPolicyMetadata(): Promise<PrecisionPolicyMetadata> {
  return apiGet<PrecisionPolicyMetadata>(`${BASE}/metadata`);
}

export function isPrecisionPolicyLoaded(): boolean {
  return _snapshot.loaded;
}

/** Olvida la política (logout / cambio de empresa) e invalida todo load/save en vuelo. */
export function clearPrecisionPolicy(): void {
  _generation++;
  publishPolicy(null);
}

/** Política cargada de la empresa activa. Lanza `PrecisionPolicyNotLoadedError` si aún no se cargó. */
export function getPrecisionPolicy(): PrecisionPolicy {
  if (!_snapshot.loaded) throw new PrecisionPolicyNotLoadedError();
  return _snapshot.policy;
}

/** Solo tests: fija la política sin pasar por la API (mismo estado único; invalida lo que esté en vuelo). */
export function setPrecisionPolicyForTests(policy: PrecisionPolicy | null): void {
  _generation++;
  publishPolicy(policy);
}

export type UpdatePrecisionPolicyInput = Omit<
  PrecisionPolicy,
  "isLocked" | "lockedAt" | "lockedReason" | "moneyDecimals" | "taxDecimals" | "accountingDecimals" | "fiscalPercentageDecimals"
>;

/**
 * Guarda y publica la respuesta real del backend (sin estado optimista). Invalida los loads
 * anteriores y los iniciados mientras está en vuelo; si un clear o un save posterior lo dejó
 * obsoleto, devuelve el resultado sin publicarlo.
 */
export async function savePrecisionPolicy(
  input: UpdatePrecisionPolicyInput,
): Promise<PrecisionPolicy> {
  const generation = ++_generation;
  _pendingSaveGeneration = generation;
  try {
    const result = await apiPut<PrecisionPolicy>(BASE, input);
    if (generation === _generation) publishPolicy(result);
    return result;
  } finally {
    if (_pendingSaveGeneration === generation) _pendingSaveGeneration = null;
  }
}
