import { apiGet } from "../../lib/apiEnvelope";

const BASE = "/api/v1/sales";

export type ConsumerFinalMaxAmountSource =
  | "Manual"
  | "TaxRegimeDefault"
  | "Fallback";

/**
 * Política fiscal efectiva de Consumidor Final, resuelta por el backend (autoridad única —
 * ver ISalesFiscalPolicyResolver). blockConsumerFinalCredit siempre es `true`; maxInvoiceAmount
 * nunca se hardcodea en frontend, viaja siempre desde aquí.
 */
export interface SalesConsumerFinalPolicyDto {
  blockConsumerFinalCredit: boolean;
  consumerFinalMaxAmount: number;
  consumerFinalMaxAmountSource: ConsumerFinalMaxAmountSource;
  taxRegimeCode: string | null;
  creditBlockedMessage: string;
  amountExceededMessage: string;
}

export interface SalesRuntimeDefaultsDto {
  defaultDocTypeCode: string | null;
  defaultSriPaymentMethodCode: string | null;
  defaultEmissionPointId: string | null;
  defaultWarehouseId: string | null;
  defaultPaymentTermId: string | null;
  fallbackDocTypeCode: string;
  fallbackSriPaymentMethodCode: string;
}

export interface SalesRuntimeContextDto {
  consumerFinalPolicy: SalesConsumerFinalPolicyDto;
  defaults: SalesRuntimeDefaultsDto;
}

export const salesRuntimeContextService = {
  /** Contexto agregado (política fiscal + defaults) a precargar al abrir Ventas. */
  get: () => apiGet<SalesRuntimeContextDto>(`${BASE}/runtime-context`),
};
