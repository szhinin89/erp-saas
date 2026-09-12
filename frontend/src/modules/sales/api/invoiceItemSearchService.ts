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
  /** SALES-PRICE-LIST-DISCOUNT-VISIBILITY-01: presentes solo cuando la lista de precios default
   * aplica un descuento/recargo sobre este ítem — salePriceWithoutTax/finalSalePrice de arriba
   * siguen siendo el precio base sin resolver. Null = sin ajuste, el precio final real es el
   * ya mostrado arriba. */
  priceListName: string | null;
  discountDescription: string | null;
  discountedSalePriceWithoutTax: number | null;
  discountedFinalSalePrice: number | null;
}

export const invoiceItemSearchService = {
  search: (params: { q: string; warehouseId?: string; pageSize?: number }) =>
    apiGet<InvoiceItemSearchResultDto[]>(`${BASE}/item-search`, { params }),
};
