import { apiGet, apiPatch, apiPost, apiPut } from "../../lib/apiEnvelope";
import type {
  ItemDetailDto,
  ItemDto,
  ItemVariantDto,
  GetItemsResponse,
} from "../../../types/items";
import type { PricingRuleSummaryDto } from "../../pricing/types";

export interface GetItemsParams {
  search?: string;
  sku?: string;
  isActive?: boolean;
  isForSale?: boolean;
  isFavorite?: boolean;
  isEcommerce?: boolean;
  itemTypeId?: string;
  categoryNodeId?: string;
  brandId?: string;
  barcode?: string;
  pageNumber?: number;
  pageSize?: number;
}

export interface CreateItemBarcodeRequest {
  code: string;
  barcodeType: string;
  isPrimary: boolean;
}

export interface CreateItemSupplierCodeRequest {
  code: string;
  isPrimary: boolean;
  // Obligatorio (Fase 2) — no existe código de proveedor sin proveedor.
  supplierId: string;
}

export interface CreateItemRequest {
  sku: string;
  shortName: string;
  description: string;
  itemTypeId: string;
  defaultUomCode: string;
  categoryNodeId: string;
  brandId: string;
  barcodes: CreateItemBarcodeRequest[];
  // NOTA: no existen campos de cuenta contable (vatAccountId/purchaseVatAccountId/
  // exciseAccountId) ni sriServiceCode — no forman parte del contrato.
  saleVatCode: string | null;
  purchaseVatCode: string | null;
  observations?: string | null;
  supplierCodes?: CreateItemSupplierCodeRequest[];
  // SSOT del precio base del ítem (ADR-021) — siempre presente en el payload, nunca
  // omitido: `null` es "sin precio aún", nunca "no lo toqué".
  baseSalePrice: number | null;
  exciseTaxCode?: string | null;
  isForSale?: boolean;
  maxDiscountPercent?: number | null;
  isAvailableOnWeb?: boolean;
  isAvailableOnPOS?: boolean;
  isAvailableOnMobile?: boolean;
  isEcommerceActive?: boolean;
  tracksStock?: boolean;
  tracksLot?: boolean;
  tracksSeries?: boolean;
  allowDecimalQty?: boolean;
  allowDecimalSale?: boolean;
  minStockQty?: number | null;
  maxStockQty?: number | null;
}

export interface UpdateItemRequest extends Omit<
  CreateItemRequest,
  "barcodes" | "supplierCodes" | "categoryNodeId" | "brandId" | "itemTypeId"
> {
  id: string;
  // SKU es editable (Fase 1) — único por tenant, excluyendo el propio ítem.
  sku: string;
  categoryNodeId?: string | null;
  brandId?: string | null;
  // Obligatorio en Update (UpdateItemCommandValidator.NotNull) — jamás se omite,
  // así el backend nunca puede confundir "no lo toqué" con "bórralo".
  baseSalePrice: number;
}

export interface AddVariantRequest {
  attributes: { attributeDefinitionId: string; value: string }[];
  skuOverride?: string | null;
  sortOrder?: number;
}

function buildParams(params: GetItemsParams): string {
  const q = new URLSearchParams();
  if (params.search) q.set("search", params.search);
  if (params.sku) q.set("sku", params.sku);
  if (params.isActive !== undefined) q.set("isActive", String(params.isActive));
  if (params.isForSale !== undefined)
    q.set("isForSale", String(params.isForSale));
  if (params.isFavorite !== undefined)
    q.set("isFavorite", String(params.isFavorite));
  if (params.isEcommerce !== undefined)
    q.set("isEcommerce", String(params.isEcommerce));
  if (params.itemTypeId) q.set("itemTypeId", params.itemTypeId);
  if (params.categoryNodeId) q.set("categoryNodeId", params.categoryNodeId);
  if (params.brandId) q.set("brandId", params.brandId);
  if (params.barcode) q.set("barcode", params.barcode);
  q.set("pageNumber", String(params.pageNumber ?? 1));
  q.set("pageSize", String(params.pageSize ?? 20));
  return q.toString() ? `?${q.toString()}` : "";
}

export const itemService = {
  getAll: (params: GetItemsParams = {}) =>
    apiGet<GetItemsResponse>(`/api/v1/items${buildParams(params)}`),

  getById: (id: string) => apiGet<ItemDetailDto>(`/api/v1/items/${id}`),

  create: (request: CreateItemRequest) =>
    apiPost<ItemDto>("/api/v1/items", request),

  update: (request: UpdateItemRequest) => {
    const { id, ...body } = request;
    return apiPut<ItemDto>(`/api/v1/items/${id}`, { id, ...body });
  },

  disable: (id: string) => apiPatch<boolean>(`/api/v1/items/${id}/disable`),
  enable: (id: string) => apiPatch<boolean>(`/api/v1/items/${id}/enable`),

  addVariant: (itemId: string, request: AddVariantRequest) =>
    apiPost<ItemVariantDto>(`/api/v1/items/${itemId}/variants`, request),

  updateVariant: (
    itemId: string,
    variantId: string,
    request: AddVariantRequest,
  ) =>
    apiPut<ItemVariantDto>(
      `/api/v1/items/${itemId}/variants/${variantId}`,
      request,
    ),

  disableVariant: (itemId: string, variantId: string) =>
    apiPatch<boolean>(`/api/v1/items/${itemId}/variants/${variantId}/disable`),

  enableVariant: (itemId: string, variantId: string) =>
    apiPatch<boolean>(`/api/v1/items/${itemId}/variants/${variantId}/enable`),

  replaceImages: (
    itemId: string,
    images: {
      storageObjectId: string;
      altText?: string | null;
      isMain?: boolean;
      isEcommerce?: boolean;
      sortOrder?: number;
      variantId?: string | null;
    }[],
  ) => apiPut<ItemDetailDto>(`/api/v1/items/${itemId}/images`, { images }),

  disableImage: (itemId: string, imageId: string) =>
    apiPatch<boolean>(`/api/v1/items/${itemId}/images/${imageId}/disable`),

  replaceUnitConversions: (
    itemId: string,
    conversions: { fromUomCode: string; toUomCode: string; factor: number }[],
  ) =>
    apiPut<ItemDetailDto>(`/api/v1/items/${itemId}/unit-conversions`, {
      conversions,
    }),

  replaceSubstitutes: (
    itemId: string,
    substitutes: {
      substituteItemId: string;
      priority: number;
      note?: string | null;
    }[],
  ) =>
    apiPut<ItemDetailDto>(`/api/v1/items/${itemId}/substitutes`, {
      substitutes,
    }),

  replacePackagingLevels: (
    itemId: string,
    levels: {
      name: string;
      level: number;
      baseQuantity: number;
      uomCode: string;
      barcode?: string | null;
      weight?: number | null;
      isBaseUnit?: boolean;
      isPurchaseDefault?: boolean;
      isSaleDefault?: boolean;
    }[],
  ) =>
    apiPut<ItemDetailDto>(`/api/v1/items/${itemId}/packaging-levels`, {
      levels,
    }),

  addBarcode: (
    itemId: string,
    variantId: string,
    code: string,
    barcodeType: string,
  ) =>
    apiPost<{ id: string; code: string; barcodeType: string }>(
      `/api/v1/items/${itemId}/variants/${variantId}/barcodes`,
      { code, barcodeType },
    ),

  disableBarcode: (itemId: string, variantId: string, barcodeId: string) =>
    apiPatch<boolean>(
      `/api/v1/items/${itemId}/variants/${variantId}/barcodes/${barcodeId}/disable`,
    ),

  getProfitability: (itemId: string) =>
    apiGet<ItemProfitabilityDto>(`/api/v1/items/${itemId}/profitability`),

  simulatePrice: (itemId: string, newPvp: number) =>
    apiPost<PriceSimulationDto>(`/api/v1/items/${itemId}/simulate-price`, {
      newPvp,
    }),

  /** Listas de precios a las que pertenece el ítem — asignación administrativa, sin reglas ni precios. */
  getPriceLists: (itemId: string) =>
    apiGet<string[]>(`/api/v1/items/${itemId}/price-lists`),

  setPriceLists: (itemId: string, priceListIds: string[]) =>
    apiPut<string[]>(`/api/v1/items/${itemId}/price-lists`, { priceListIds }),

  /**
   * Simulación de precio contra todas las listas activas y vigentes del ítem — 100% calculada
   * en el backend (misma precedencia de reglas que PricingResolver). Los overrides son
   * "qué pasaría si" (valores aún no guardados del formulario); si se omiten, usa lo persistido.
   * No persiste nada.
   */
  getPricingSimulation: (
    itemId: string,
    overrides: PricingSimulationOverrides = {},
  ) =>
    apiPost<ItemPricingSimulationRow[]>(
      `/api/v1/items/${itemId}/pricing-simulation`,
      overrides,
    ),

  /**
   * Misma simulación, para un ítem que todavía no existe (formulario de creación) — sin
   * reglas por-ítem ni asignaciones posibles, solo reglas generales de cada lista.
   * `baseSalePrice` es obligatorio aquí (no hay ítem persistido del cual heredarlo).
   */
  previewPricingSimulation: (overrides: PricingSimulationOverrides) =>
    apiPost<ItemPricingSimulationRow[]>(
      "/api/v1/items/pricing-simulation-preview",
      overrides,
    ),
};

export interface PricingSimulationOverrides {
  baseSalePrice?: number | null;
  maxDiscountPercent?: number | null;
}

/** netPrice/minAllowedPrice son precios ANTES de impuestos — Pricing nunca calcula IVA/ICE
 *  (frontera con la infraestructura tributaria). Si se necesita un precio final con impuestos,
 *  se calcula en la capa que sí es dueña de esa lógica, nunca aquí. */
export interface ItemPricingSimulationRow {
  priceListId: string;
  priceListCode: string;
  priceListName: string;
  currencyCode: string;
  isAssigned: boolean;
  /** Encapsula origen (Precio Base/Regla General/Excepción) + descripción — nunca RuleType/RuleValue sueltos. */
  ruleSummary: PricingRuleSummaryDto;
  netPrice: number;
  maxDiscountPercent: number | null;
  minAllowedPrice: number;
}

// Refleja los 5 códigos que produce ItemProfitabilityUseCases.CalcMargin() (backend) —
// única función que decide estos valores. El label/color de cada uno se resuelve en
// tiempo de ejecución vía el catálogo `item-margin-statuses`, no aquí.
export type MarginStatusCode =
  "SALUDABLE" | "BAJO" | "NEGATIVO" | "CERO" | "SIN_PRECIO";

export interface ItemProfitabilityDto {
  itemId: string;
  sku: string;
  itemName: string;
  totalStockQuantity: number;
  totalStockValue: number;
  averageCost: number;
  currentSalePrice: number;
  priceListName: string | null;
  currencyCode: string | null;
  marginAmount: number;
  marginPercent: number;
  marginStatus: MarginStatusCode;
}

export interface PriceSimulationDto {
  itemId: string;
  averageCost: number;
  currentSalePrice: number;
  currencyCode: string | null;
  currentMarginAmount: number;
  currentMarginPercent: number;
  simulatedPrice: number;
  simulatedMarginAmount: number;
  simulatedMarginPercent: number;
  marginDifference: number;
  simulatedMarginStatus: MarginStatusCode;
}
