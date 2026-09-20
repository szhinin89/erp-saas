import { apiPost } from "../../lib/apiEnvelope";

const BASE = "/api/v1/sales";

/** "Customer" | "CompanyDefault" — de qué candidato salió la lista usada (ver
 * PriceListSelectionSource en backend). Null cuando no aplicó ninguna lista (PVP). */
export type PriceListSelectionSourceDto = "Customer" | "CompanyDefault" | null;

export interface SalesRepricingPreviewItemDto {
  itemId: string;
  oldResolvedPrice: number;
  newResolvedPrice: number;
  changed: boolean;
  oldPriceListId: string | null;
  oldPriceListName: string;
  newPriceListId: string | null;
  newPriceListName: string;
  oldSelectionSource: PriceListSelectionSourceDto;
  newSelectionSource: PriceListSelectionSourceDto;
  newDiscountDescription: string | null;
}

export interface PreviewSalesRepricingRequest {
  itemIds: string[];
  oldCustomerId?: string | null;
  newCustomerId?: string | null;
}

/**
 * SALES-CUSTOMER-REPRICING-PREVIEW-06C1: previsualiza cómo cambiaría el precio resuelto de un
 * conjunto de ítems si el cliente pasara de oldCustomerId a newCustomerId — SOLO lectura, no
 * aplica ningún cambio. Todavía no consumido desde ningún flujo (handleCustomerChange sigue sin
 * tocar, sin modal) — esta fase solo agrega el service.
 */
export const salesRepricingPreviewService = {
  preview: (request: PreviewSalesRepricingRequest) =>
    apiPost<SalesRepricingPreviewItemDto[]>(
      `${BASE}/pricing/repricing-preview`,
      request,
    ),
};
