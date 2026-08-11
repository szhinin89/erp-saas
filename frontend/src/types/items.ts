// ── Core Item ──────────────────────────────────────────────────────────────

export interface ItemDto {
  id: string;
  sku: string;
  shortName: string;
  description: string;
  itemTypeId: string;
  itemTypeName: string;
  categoryNodeId: string | null;
  brandId: string | null;
  defaultUomCode: string;
  defaultUomAbbrev: string;
  isForSale: boolean;
  isFavorite: boolean;
  isEcommerceActive: boolean;
  tracksStock: boolean;
  tracksLot: boolean;
  tracksSeries: boolean;
  // PVP — SSOT del precio base del ítem (ADR-021, Pricing Engine v2). Nunca se deriva
  // de PriceList/PricingRule; vive exclusivamente en Item.
  baseSalePrice: number | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

// NOTA: no existen campos de cuenta contable (vatAccountId/purchaseVatAccountId/
// exciseAccountId) ni sriServiceCode — no forman parte del contrato público.
export interface ItemTaxConfigDto {
  saleVatCode: string | null;
  saleVatName: string | null;
  purchaseVatCode: string | null;
  purchaseVatName: string | null;
  exciseTaxCode: string | null;
  exciseTaxName: string | null;
}

export interface ItemSaleConfigDto {
  isForSale: boolean;
  maxDiscountPercent: number | null;
  isAvailableOnWeb: boolean;
  isAvailableOnPOS: boolean;
  isAvailableOnMobile: boolean;
  isEcommerceActive: boolean;
  isFavorite: boolean;
}

export interface ItemStockConfigDto {
  tracksStock: boolean;
  tracksLot: boolean;
  tracksSeries: boolean;
  allowDecimalQty: boolean;
  allowDecimalSale: boolean;
  minStockQty: number | null;
  maxStockQty: number | null;
}

export interface ItemVariantDto {
  id: string;
  sku: string;
  name: string;
  isDefault: boolean;
  sortOrder: number;
  isActive: boolean;
  attributes: VariantAttributeDto[];
  barcodes: VariantBarcodeDto[];
}

export interface VariantAttributeDto {
  attributeDefinitionId: string;
  value: string;
}

export interface VariantBarcodeDto {
  id: string;
  code: string;
  barcodeType: string;
  isPrimary: boolean;
}

export interface ItemImageDto {
  id: string;
  variantId: string | null;
  storageObjectId: string;
  altText: string | null;
  isMain: boolean;
  isEcommerce: boolean;
  sortOrder: number;
  isActive: boolean;
}

export interface ItemUnitConversionDto {
  id: string;
  fromUomCode: string;
  fromUomAbbrev: string;
  toUomCode: string;
  toUomAbbrev: string;
  factor: number;
  isActive: boolean;
}

export interface ItemSubstituteDto {
  id: string;
  substituteItemId: string;
  priority: number;
  note: string | null;
  isActive: boolean;
}

export interface ItemPackagingLevelDto {
  id: string;
  name: string;
  level: number;
  baseQuantity: number;
  uomCode: string;
  uomAbbrev: string;
  barcode: string | null;
  weight: number | null;
  isBaseUnit: boolean;
  isPurchaseDefault: boolean;
  isSaleDefault: boolean;
  isActive: boolean;
}

export interface ItemSupplierCodeDto {
  id: string;
  supplierId: string | null;
  packagingLevelId: string | null;
  code: string;
  isPrimary: boolean;
  isActive: boolean;
}

export interface ItemDetailDto extends ItemDto {
  defaultUomName: string;
  observations: string | null;
  taxConfig: ItemTaxConfigDto;
  saleConfig: ItemSaleConfigDto;
  stockConfig: ItemStockConfigDto;
  variants: ItemVariantDto[];
  images: ItemImageDto[];
  unitConversions: ItemUnitConversionDto[];
  substitutes: ItemSubstituteDto[];
  packagingLevels: ItemPackagingLevelDto[];
  supplierCodes: ItemSupplierCodeDto[];
}

export interface GetItemsResponse {
  items: ItemDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface ResolvedPriceDto {
  itemId: string;
  variantId: string | null;
  uomCode: string;
  basePrice: number;
  discountAmount: number;
  finalPrice: number;
  priceIncludesVat: boolean;
  appliedPriceListId: string;
  appliedPriceListName: string;
  resolutionPath: string;
}
