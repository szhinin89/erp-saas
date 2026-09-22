import {
  apiGet,
  apiPatch,
  apiPost,
  apiPut,
  apiDelete,
} from "../../lib/apiEnvelope";
import { getPrecisionPolicy } from "../../../lib/config/precisionPolicy.config";
import { formatMoney } from "../../../lib/sanitizers";

const BASE = "/api/v1/pricing";

// ── DTOs ─────────────────────────────────────────────────────────────────

export interface PriceListDto {
  id: string;
  code: string;
  name: string;
  currencyCode: string;
  isDefault: boolean;
  validFrom: string | null;
  validUntil: string | null;
  ruleType: string | null;
  ruleValue: number | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

/** Excepción de precio (PricingRule) — CRUD crudo, RuleType/RuleValue son correctos aquí porque
 *  es lo que se está editando. Para "por qué se resolvió este precio" ver PricingRuleSummaryDto.
 *  No soporta pricing por variante — ver PricingRule.cs (retirado deliberadamente, auditoría
 *  de cierre 2026-07-07). */
export interface PricingRuleDto {
  id: string;
  priceListId: string;
  itemId: string;
  ruleType: string;
  ruleValue: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
  /** Auditoría de dominio (PricingRuleAudit vía IAuditReader) — no UserActivity. */
  lastModifiedAt: string | null;
  lastModifiedByName: string | null;
}

export interface SetPricingRulePayload {
  priceListId: string;
  itemId: string;
  ruleType: string;
  ruleValue: number;
}

export interface EnablePricingRulePayload {
  priceListId: string;
  itemId: string;
}

/** Espejo de PricingRuleSetStatus (backend) — nunca se infiere en el cliente. */
export type PricingRuleSetStatus =
  "Created" | "AlreadyActive" | "ExistsInactive";

/** Resultado de pricingRuleService.set — "ExistsInactive" exige confirmación explícita del
 *  usuario antes de reactivar (ver pricingRuleService.enable); nunca se reactiva sola.
 *  existingRuleType/existingRuleValue solo vienen poblados cuando status es "ExistsInactive" —
 *  permiten mostrar el valor de la excepción deshabilitada antes de confirmar la reactivación. */
export interface PricingRuleSetResultDto {
  status: PricingRuleSetStatus;
  pricingRuleId: string | null;
  existingRuleType: string | null;
  existingRuleValue: number | null;
}

/** Ítem perteneciente a una PriceList — identidad + precio base únicamente (sin reglas). */
export interface PriceListAssignedItemDto {
  itemId: string;
  sku: string;
  itemName: string;
  baseSalePrice: number | null;
}

/** Cliente asignado a una PriceList — identidad + nombre para mostrar, sin datos de pricing. */
export interface PriceListCustomerDto {
  customerId: string;
  customerName: string;
  customerIdentificationNumber: string | null;
}

/** Espejo de PriceListCustomerAssignStatus (backend) — nunca se infiere en el cliente. */
export type PriceListCustomerAssignStatus =
  "Assigned" | "AlreadyActive" | "Conflict" | "Switched";

/** Resultado de priceListCustomerService.assign — "Conflict" exige confirmación explícita del
 *  usuario antes de reintentar con confirmSwitch=true (ver PriceListCustomersTab); nunca se
 *  cambia de lista en silencio. conflictingPriceListId/Name solo vienen poblados cuando status
 *  es "Conflict". */
export interface PriceListCustomerAssignResultDto {
  status: PriceListCustomerAssignStatus;
  conflictingPriceListId: string | null;
  conflictingPriceListName: string | null;
}

/** Espejo de PriceSource (backend) — de dónde proviene el precio resuelto de un ítem en una lista. */
export type PriceSource = "BasePrice" | "GeneralRule" | "Exception";

/** Espejo de PricingRuleSummaryDto (backend) — nunca se recalcula en el cliente. */
export interface PricingRuleSummaryDto {
  source: PriceSource;
  type: string | null;
  value: number | null;
  description: string;
}

// ── Payloads ─────────────────────────────────────────────────────────────

export interface CreatePriceListPayload {
  code: string;
  name: string;
  currencyCode: string;
  isDefault: boolean;
  validFrom?: string | null;
  validUntil?: string | null;
  ruleType?: string | null;
  ruleValue?: number | null;
}

export interface UpdatePriceListPayload {
  id: string;
  name: string;
  currencyCode: string;
  isDefault: boolean;
  validFrom?: string | null;
  validUntil?: string | null;
  ruleType?: string | null;
  ruleValue?: number | null;
}

// ── Regla general (mapeo UI ↔ PricingRuleType del backend) ────────────────
// Los `value` deben coincidir exactamente con los nombres del enum PricingRuleType
// (serializado como string vía JsonStringEnumConverter) — no traducir ni renombrar.

export const RULE_TYPE_OPTIONS = [
  { value: "", label: "Ninguna" },
  { value: "PercentDiscount", label: "Descuento %" },
  { value: "PercentMarkup", label: "Recargo %" },
  { value: "FixedAdjustment", label: "Ajuste fijo" },
  { value: "FixedPrice", label: "Precio fijo" },
] as const;

const CURRENCY_SYMBOLS: Record<string, string> = {
  USD: "$",
  EUR: "€",
  GBP: "£",
};

function currencySymbol(code: string): string {
  return CURRENCY_SYMBOLS[code] ?? code;
}

/** Texto orientado al usuario para la columna "Regla General" del listado. */
export function formatRuleGeneral(
  ruleType: string | null,
  ruleValue: number | null,
  currencyCode: string,
  translate?: (key: string, params?: Record<string, string | number>) => string,
): string {
  // ERP-PRECISION-FRONTEND-06B: porcentajes → percentageDecimals; precio fijo/ajuste unitario →
  // salesUnitPriceDecimals (política de la empresa).
  const policy = getPrecisionPolicy();
  const pct = (v: number) => formatMoney(v, policy.percentageDecimals);
  const unit = (v: number) => formatMoney(v, policy.salesUnitPriceDecimals);
  if (translate) {
    if (!ruleType || ruleValue == null) return translate("pricing.ux.rule.none");
    const isPercent = ruleType === "PercentDiscount" || ruleType === "PercentMarkup";
    const value = isPercent ? pct(ruleValue)
      : ruleType === "FixedPrice" ? `${currencyCode} ${unit(ruleValue)}`
      : ruleType === "FixedAdjustment" ? `${ruleValue >= 0 ? "+" : ""}${unit(ruleValue)}`
      : ruleValue;
    return translate(`pricing.ux.rule.${ruleType}`, { value });
  }
  if (!ruleType || ruleValue == null) return "Sin regla";
  switch (ruleType) {
    case "PercentDiscount":
      return `Descuento ${pct(ruleValue)}%`;
    case "PercentMarkup":
      return `Recargo ${pct(ruleValue)}%`;
    case "FixedPrice":
      return `Precio fijo ${currencySymbol(currencyCode)}${unit(ruleValue)}`;
    case "FixedAdjustment":
      return `Ajuste ${ruleValue >= 0 ? "+" : ""}${unit(ruleValue)}`;
    default:
      return "Sin regla";
  }
}

// ── Services ─────────────────────────────────────────────────────────────

export const priceListService = {
  list: (isActive?: boolean, search?: string) => {
    const params = new URLSearchParams();
    if (isActive !== undefined) params.set("isActive", String(isActive));
    if (search?.trim()) params.set("search", search.trim());
    const qs = params.toString();
    return apiGet<PriceListDto[]>(`${BASE}/price-lists${qs ? `?${qs}` : ""}`);
  },
  getById: (id: string) => apiGet<PriceListDto>(`${BASE}/price-lists/${id}`),
  create: (p: CreatePriceListPayload) =>
    apiPost<PriceListDto>(`${BASE}/price-lists`, p),
  update: (id: string, p: UpdatePriceListPayload) =>
    apiPut<PriceListDto>(`${BASE}/price-lists/${id}`, p),
  enable: (id: string) => apiPatch<boolean>(`${BASE}/price-lists/${id}/enable`),
  disable: (id: string) =>
    apiPatch<boolean>(`${BASE}/price-lists/${id}/disable`),
  /** Único punto de escritura para el default — usado desde Configuración → Ventas. */
  setDefault: (id: string) =>
    apiPatch<PriceListDto>(`${BASE}/price-lists/${id}/set-default`),
  /** Responsabilidad única: qué ítems pertenecen a esta lista (sin reglas ni excepciones). */
  getAssignedItems: (id: string) =>
    apiGet<PriceListAssignedItemDto[]>(
      `${BASE}/price-lists/${id}/assigned-items`,
    ),
};

/** PRICING-CUSTOMER-PRICE-LIST-ADMIN-05B — administración de clientes por lista de precios.
 *  Todavía no consumido por Sales; solo /products/pricing → tab "Clientes de la lista". */
export const priceListCustomerService = {
  list: (priceListId: string) =>
    apiGet<PriceListCustomerDto[]>(
      `${BASE}/price-lists/${priceListId}/customers`,
    ),
  /** Si status="Conflict", no se escribió nada — reintentar con confirmSwitch=true tras
   *  confirmación explícita del usuario para completar el cambio de lista. */
  assign: (priceListId: string, customerId: string, confirmSwitch = false) =>
    apiPost<PriceListCustomerAssignResultDto>(
      `${BASE}/price-lists/${priceListId}/customers`,
      { customerId, confirmSwitch },
    ),
  remove: (priceListId: string, customerId: string) =>
    apiDelete<boolean>(
      `${BASE}/price-lists/${priceListId}/customers/${customerId}`,
    ),
};

/** Administración de excepciones (PricingRule) por ítem dentro de una PriceList. */
export const pricingRuleService = {
  /** Trae las excepciones de una lista o de un ítem — exactamente uno de los dos parámetros. */
  list: (priceListId?: string, itemId?: string) => {
    const params = new URLSearchParams();
    if (priceListId) params.set("priceListId", priceListId);
    if (itemId) params.set("itemId", itemId);
    return apiGet<PricingRuleDto[]>(
      `${BASE}/pricing-rules?${params.toString()}`,
    );
  },
  /**
   * Crea una excepción nueva o actualiza el valor de una ya activa. Si ya existe una
   * deshabilitada con la misma clave, NO la reactiva sola — devuelve status="ExistsInactive"
   * para que la UI pida confirmación y llame a `enable`.
   */
  set: (p: SetPricingRulePayload) =>
    apiPost<PricingRuleSetResultDto>(`${BASE}/pricing-rules`, p),
  /** Reactivación explícita — único camino oficial para volver a activar una excepción deshabilitada. */
  enable: (p: EnablePricingRulePayload) =>
    apiPost<PricingRuleDto>(`${BASE}/pricing-rules/enable`, p),
  remove: (id: string) => apiDelete<boolean>(`${BASE}/pricing-rules/${id}`),
};

/** Existing read-only simulation; prices and draft precedence are resolved by the backend. */
export const pricingSimulationService = {
  simulate: (itemId: string, exceptionPreview?: SetPricingRulePayload) =>
    apiPost<{ priceListId: string; netPrice: number; ruleSummary: PricingRuleSummaryDto }[]>(
      `/api/v1/items/${itemId}/pricing-simulation`, { exceptionPreview }),
};
