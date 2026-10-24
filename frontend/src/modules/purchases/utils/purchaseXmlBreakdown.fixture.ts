import type { PurchaseDraftLineDto } from "../api/purchaseReceptionService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";

// Promotional XML line: gross 12.00, discount 2.00, IVA 15% base 10.00 / 1.50, ICE base 10.00 / 0.40.
export const xml: PurchaseDraftLineDto = {
  purchaseReceptionLineId: "rx-line-1",
  itemId: null,
  itemMatchStatus: "PENDING",
  description: "COMBO PROMO",
  quantity: 1,
  unitPrice: 12,
  vatCode: "4",
  warehouseId: "wh-1",
  notes: null,
  discountPct: 0,
  iceCode: "3011",
  supplierCode: "13563",
  supplierAuxCode: null,
  discount: 2,
  lineSubtotal: 10,
  taxCode: "2",
  vatPercentage: 15,
  taxValue: 1.5,
  totalLine: 11.9,
  packagingLevelId: null,
  uomCode: "UNIT",
  baseUomCode: "UNIT",
  conversionFactor: 1,
  quantityInBaseUom: 1,
  taxes: [
    { purchaseReceptionLineTaxId: "tax-vat", taxCode: "2", taxRateCode: "4", tarifa: 15, taxableBase: 10, taxAmount: 1.5 },
    { purchaseReceptionLineTaxId: "tax-ice", taxCode: "3", taxRateCode: "3011", tarifa: 4, taxableBase: 10, taxAmount: 0.4 },
  ],
  additionalFields: [],
};

export function promo(overrides: Partial<PurchaseLineFormValues> = {}): PurchaseLineFormValues {
  return {
    _key: 2, description: xml.description, quantity: 1, unitPrice: 12, vatCode: "4", discountPct: 0,
    purchaseReceptionLineId: xml.purchaseReceptionLineId, warehouseId: "wh-1", xmlSource: xml, ...overrides,
  };
}

export function component(itemId: string, quantity: number, subtotal: number, discount: number,
  vat: [number, number], ice: [number, number], overrides: Partial<PurchaseLineFormValues> = {}): PurchaseLineFormValues {
  return {
    _key: 0, itemId, description: itemId, quantity, unitPrice: subtotal / quantity, vatCode: "4", discountPct: 0,
    conversionFactor: 1, purchaseReceptionLineId: xml.purchaseReceptionLineId, xmlSource: xml, warehouseId: "wh-1",
    xmlAllocation: { subtotal, discount, taxes: [
      { purchaseReceptionLineTaxId: "tax-vat", taxableBase: vat[0], taxAmount: vat[1] },
      { purchaseReceptionLineTaxId: "tax-ice", taxableBase: ice[0], taxAmount: ice[1] },
    ] },
    ...overrides,
  };
}

export const balanced = () => [
  component("item-a", 2, 8, 1.5, [6.5, 0.98], [6.5, 0.26]),
  component("item-b", 1, 4, 0.5, [3.5, 0.52], [3.5, 0.14]),
];

export const manual: PurchaseLineFormValues = {
  _key: 1, description: "Manual", quantity: 1, unitPrice: 5, vatCode: "4", discountPct: 0,
};
