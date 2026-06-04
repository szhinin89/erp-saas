// ── Core Item ──────────────────────────────────────────────────────────────

export type ItemType = 'Physical' | 'Service' | 'Digital' | 'Kit' | 'Bundle';

export interface ItemDto {
  id: string;
  sku: string;
  purchaseCode: string | null;
  shortName: string;
  description: string;
  itemType: ItemType;
  categoryNodeId: string | null;
  brandId: string | null;
  defaultUomCode: string;
  isForSale: boolean;
  isFavorite: boolean;
  isEcommerceActive: boolean;
  tracksStock: boolean;
  tracksLot: boolean;
  tracksSeries: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface ItemTaxConfigDto {
  appliesVatOnSale: boolean;
  appliesVatOnPurchase: boolean;
  appliesExciseTax: boolean;
  saleVatCode: string | null;
  purchaseVatCode: string | null;
  exciseTaxCode: string | null;
  vatAccountId: string | null;
  purchaseVatAccountId: string | null;
  exciseAccountId: string | null;
  sriServiceCode: string | null;
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
  toUomCode: string;
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
  barcode: string | null;
  weight: number | null;
  isBaseUnit: boolean;
  isPurchaseDefault: boolean;
  isSaleDefault: boolean;
  isActive: boolean;
}

export interface ItemDetailDto extends ItemDto {
  observations: string | null;
  taxConfig: ItemTaxConfigDto;
  saleConfig: ItemSaleConfigDto;
  stockConfig: ItemStockConfigDto;
  variants: ItemVariantDto[];
  images: ItemImageDto[];
  unitConversions: ItemUnitConversionDto[];
  substitutes: ItemSubstituteDto[];
  packagingLevels: ItemPackagingLevelDto[];
}

export interface GetItemsResponse {
  items: ItemDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

// ── Catalog ────────────────────────────────────────────────────────────────

export interface AttributeGroupDto {
  id: string;
  code: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
}

export interface AttributeDefinitionDto {
  id: string;
  groupId: string;
  code: string;
  name: string;
  dataType: string;
  isVariantAxis: boolean;
  allowedValues: string | null;
  isRequired: boolean;
  sortOrder: number;
  isActive: boolean;
}

// ── Inventory ──────────────────────────────────────────────────────────────

export interface LotDto {
  id: string;
  lotNumber: string;
  itemId: string;
  variantId: string | null;
  warehouseId: string;
  manufactureDate: string | null;
  expirationDate: string | null;
  initialQty: number;
  currentQty: number;
  status: string;
  notes: string | null;
  createdAt: string;
}

export interface SerialDto {
  id: string;
  serial: string;
  itemId: string;
  variantId: string | null;
  warehouseId: string;
  lotId: string | null;
  status: string;
  acquiredAt: string;
  soldAt: string | null;
  documentRef: string | null;
}

// ── Pricing ────────────────────────────────────────────────────────────────

export interface PriceListDto {
  id: string;
  code: string;
  name: string;
  currencyCode: string;
  priceIncludesVat: boolean;
  validFrom: string;
  validTo: string | null;
  isDefault: boolean;
  target: string;
  isActive: boolean;
  createdAt: string;
}

export interface PriceListEntryDto {
  id: string;
  itemId: string;
  variantId: string | null;
  uomCode: string;
  price: number;
  minQty: number;
  isActive: boolean;
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

// ── Kits ───────────────────────────────────────────────────────────────────

export interface KitDto {
  id: string;
  itemId: string;
  kitType: string;
  isActive: boolean;
  lines: KitLineDto[];
}

export interface KitLineDto {
  id: string;
  componentItemId: string;
  componentVariantId: string | null;
  quantity: number;
  uomCode: string;
  isOptional: boolean;
  sortOrder: number;
  isActive: boolean;
}

// ── Supplier Catalog ───────────────────────────────────────────────────────

export interface SupplierItemDto {
  id: string;
  supplierId: string;
  itemId: string;
  variantId: string | null;
  supplierCode: string;
  supplierName: string | null;
  defaultUomCode: string;
  leadTimeDays: number;
  minOrderQty: number;
  isDefault: boolean;
  isActive: boolean;
  prices: SupplierPriceDto[];
}

export interface SupplierPriceDto {
  id: string;
  minQty: number;
  unitPrice: number;
  currencyCode: string;
  validFrom: string | null;
  validTo: string | null;
}

// ── Digital ────────────────────────────────────────────────────────────────

export interface DigitalItemDto {
  id: string;
  itemId: string;
  licenseType: string;
  deliveryMethod: string;
  maxDownloads: number | null;
  validityDays: number | null;
  isActive: boolean;
  deliverables: DeliverableDto[];
}

export interface DeliverableDto {
  id: string;
  name: string;
  storageObjectId: string;
  version: string | null;
  isActive: boolean;
}
