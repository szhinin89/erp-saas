import { apiGet } from "../../lib/apiEnvelope";

/**
 * SALES-PRICING-UX-TRACEABILITY-07C: contexto read-only de listas de un cliente, propiedad del
 * módulo Pricing. `customerPriceList*` es SOLO la lista explícitamente asignada al cliente;
 * `companyDefaultPriceList*` es SOLO la default de la empresa — nunca se presenta la default como
 * lista del cliente. Sales no decide ni calcula listas: solo muestra lo que Pricing responde.
 */
export interface CustomerPriceListContextDto {
  customerPriceListId: string | null;
  customerPriceListName: string | null;
  companyDefaultPriceListId: string | null;
  companyDefaultPriceListName: string | null;
}

export const customerPriceListContextService = {
  get: (customerId: string) =>
    apiGet<CustomerPriceListContextDto>(
      `/api/v1/pricing/customers/${customerId}/price-list-context`,
    ),
};
