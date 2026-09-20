import { apiGet } from "../../lib/apiEnvelope";

const BASE = "/api/v1/sales";

/** SALES-PRESENTATIONS-03 — presentación vendible de un ítem (ItemPackagingLevel activo),
 * expuesta por el buscador de ventas. Espejo de PurchaseItemPackagingLevelDto (Compras); no se
 * comparte el mismo tipo TS porque cada módulo consume su propio endpoint/contrato de API. */
export interface InvoiceItemPackagingLevelDto {
  id: string;
  name: string;
  uomCode: string;
  baseQuantity: number;
  barcode: string | null;
  isBaseUnit: boolean;
  isSaleDefault: boolean;
}

export interface InvoiceItemSearchResultDto {
  id: string;
  sku: string;
  description: string;
  productFamilyName: string | null;
  uomAbbrev: string;
  tracksStock: boolean;
  warehouseName: string | null;
  availableStock: number | null;
  averageCost: number | null;
  salePriceWithoutTax: number | null;
  finalSalePrice: number | null;
  vatDisplay: string;
  iceDisplay: string;
  vatCode: string | null;
  iceCode: string | null;
  baseUomCode: string;
  packagingLevels: InvoiceItemPackagingLevelDto[];
  /** Si el texto buscado coincidió con el barcode de una presentación específica (no la unidad
   * base), esa presentación debe autoseleccionarse al agregar la línea — ver useSalesPage.ts. */
  matchedPackagingLevelId: string | null;
  /** SALES-PRICE-LIST-DISCOUNT-VISIBILITY-01 / SALES-CONTEXTUAL-PRICING-READ-06A: presentes
   * solo cuando IPricingResolver resolvió una lista (cliente o default) que aplica un
   * descuento/recargo sobre este ítem — salePriceWithoutTax/finalSalePrice de arriba siguen
   * siendo el precio base sin resolver. Null = sin ajuste, el precio final real es el ya
   * mostrado arriba. */
  priceListName: string | null;
  discountDescription: string | null;
  discountedSalePriceWithoutTax: number | null;
  discountedFinalSalePrice: number | null;
}

export const invoiceItemSearchService = {
  /** SALES-CONTEXTUAL-PRICING-READ-06A: customerId es el cliente actualmente seleccionado en la
   * venta (o undefined si aún no hay uno) — Sales solo lo informa, nunca decide qué lista de
   * precios corresponde; eso lo resuelve IPricingResolver en el backend. */
  search: (params: { q: string; warehouseId?: string; pageSize?: number; customerId?: string }) =>
    apiGet<InvoiceItemSearchResultDto[]>(`${BASE}/item-search`, { params }),
};
