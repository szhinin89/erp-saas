import type { PurchaseItemContextDto } from "../api/purchaseService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import type { PurchaseLineReadiness } from "../utils/purchaseLineReadiness";

// PURCHASE-LINE-COMPACT-01 — escenarios de la tarjeta de línea de compra (PurchaseLineCard):
// XML con ICE editada, compra manual sin datos comerciales, línea desglosada e IRBPNR.

export const lineCardContext: PurchaseItemContextDto = {
  itemId: "item-1",
  sku: "COLA-500",
  shortName: "Cola 500 ml",
  description: "COLA-500 — Cola 500 ml",
  baseUomCode: "UNIT",
  participatesInInventory: true,
  supplierCode: "13563",
  packagingLevels: [
    { id: "unit", name: "UNIDAD", baseQuantity: 1, uomCode: "UNIT", isBaseUnit: true, isPurchaseDefault: false },
    { id: "box-12", name: "CAJA x12", baseQuantity: 12, uomCode: "BOX", isBaseUnit: false, isPurchaseDefault: true },
  ],
  currentStock: 48,
  availableStock: 40,
  reservedStock: 8,
  averageCost: 0.52,
  lastPurchaseCost: 0.5,
  pvp: 0.85,
  previousPrice: 0.8,
  maxDiscountPercent: 5,
  purchaseVatCode: "4",
  vatPercent: 15,
  exciseTaxCode: "3011",
  icePercent: 10,
  hasVat: true,
  hasIce: true,
  costMargin: 0.3,
  costMarginPercent: 35,
};

/** Línea XML vinculada, en CAJA x12, con ICE y cantidad editada respecto del documento. */
export const xmlIceEditedLine: PurchaseLineFormValues = {
  _key: 1,
  itemId: "item-1",
  description: "COLA-500 — Cola 500 ml",
  quantity: 3,
  unitPrice: 6,
  vatCode: "4",
  iceCode: "3011",
  discountPct: 0,
  warehouseId: "wh-1",
  packagingLevelId: "box-12",
  uomCode: "BOX",
  baseUomCode: "UNIT",
  conversionFactor: 12,
  quantityInBaseUom: 36,
  baseUnitPrice: 0.5,
  xmlOriginPackagingLevelId: "box-12",
  xmlOriginFactor: 12,
  purchaseReceptionLineId: "rl-1",
  itemMatchStatus: "AUTO_MATCHED",
  context: lineCardContext,
  xmlSupplierCode: "13563",
  xmlSupplierAuxCode: "7861234500017",
  xmlDescription: "COLA 500ML CAJA X12",
  xmlQuantity: 2,
  xmlUnitPrice: 6,
  xmlDiscount: 0,
  xmlLineSubtotal: 12,
  xmlTaxCode: "4",
  xmlVatPercentage: 15,
  xmlTaxableBase: 13.2,
  xmlTaxValue: 1.98,
  xmlIceAmount: 1.2,
  xmlTotalLine: 15.18,
};

/** Compra manual: sin origen XML, IVA 0 %, sin contexto comercial. */
export const manualLineWithoutCommercialData: PurchaseLineFormValues = {
  _key: 2,
  itemId: "item-2",
  description: "SERV-01 — Servicio de transporte",
  quantity: 1,
  unitPrice: 25,
  vatCode: "0",
  discountPct: 0,
  warehouseId: "wh-1",
};

/** Componente de un desglose XML (xmlAllocation): importes bloqueados, edición vía el modal. */
export const brokenDownLine: PurchaseLineFormValues = {
  ...xmlIceEditedLine,
  _key: 3,
  quantity: 2,
  quantityInBaseUom: 24,
  xmlAllocation: { subtotal: 12, discount: 0, taxes: [] },
  itemMatchStatus: "MANUALLY_MATCHED",
};

/** Línea XML con IRBPNR (botellas plásticas), sin ICE. */
export const xmlIrbpnrLine: PurchaseLineFormValues = {
  ...xmlIceEditedLine,
  _key: 4,
  iceCode: null,
  quantity: 2,
  quantityInBaseUom: 24,
  xmlIceAmount: 0,
  xmlIrbpnrAmount: 0.48,
  xmlTaxableBase: 12,
  xmlTaxValue: 1.8,
  xmlTotalLine: 14.28,
  context: { ...lineCardContext, exciseTaxCode: null, icePercent: 0, hasIce: false },
};

/** Línea XML aún sin producto vinculado (sin contexto ni presentación). */
export const unlinkedXmlLine: PurchaseLineFormValues = {
  ...xmlIceEditedLine,
  _key: 5,
  itemId: null,
  description: "COLA 500ML CAJA X12",
  packagingLevelId: null,
  conversionFactor: 1,
  quantity: 2,
  quantityInBaseUom: 2,
  baseUnitPrice: 6,
  xmlOriginPackagingLevelId: null,
  xmlOriginFactor: 1,
  itemMatchStatus: "PENDING",
  context: undefined,
};

export const missingItemReadiness: PurchaseLineReadiness = {
  status: "MISSING_ITEM",
  label: "Sin producto",
  detail: "Vincule o cree el producto.",
  tone: "danger",
  blocking: true,
  primaryAction: "SELECT_ITEM",
  secondaryAction: "CREATE_ITEM",
};

export const missingWarehouseReadiness: PurchaseLineReadiness = {
  status: "MISSING_WAREHOUSE",
  label: "Falta bodega",
  detail: "Seleccione la bodega de ingreso.",
  tone: "danger",
  blocking: true,
  primaryAction: "SELECT_WAREHOUSE",
  warning: {
    status: "MISSING_SALE_PRICE_FOR_MARGIN",
    label: "Sin precio de venta",
    detail: "Configure el precio de venta para evaluar el margen.",
    tone: "warning",
  },
};
