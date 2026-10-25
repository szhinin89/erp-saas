// Fixtures y helpers compartidos por las pruebas de ItemEditorModal. Los vi.mock viven en cada
// archivo *.test.tsx (Vitest solo los eleva dentro del propio archivo de prueba).
import { expect, vi } from "vitest";
import { render, waitFor, fireEvent } from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";
import { ItemEditorModal } from "../ItemEditorModal";
import { apiGet } from "../../../../modules/lib/apiEnvelope";
import type { CreateItemInitialData, ItemCreatedResult } from "../types";
import type { ItemDetailDto } from "../../../../types/items";

export const existingItem: ItemDetailDto = {
  id: "item-1",
  sku: "SKU-1",
  shortName: "Aceite",
  description: "Aceite girasol",
  itemTypeId: "type-1",
  itemTypeName: "Bien",
  categoryNodeId: "cat-1",
  brandId: "brand-1",
  defaultUomCode: "UNIT",
  defaultUomAbbrev: "U",
  isForSale: true,
  isFavorite: false,
  isEcommerceActive: false,
  participatesInInventory: true,
  stockControlEnabled: true,
  nature: "Product",
  companyId: "company-1",
  tracksLot: false,
  tracksSeries: false,
  baseSalePrice: 10,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
  defaultUomName: "Unidad",
  observations: null,
  taxConfig: {
    saleVatCode: "2",
    saleVatName: "IVA 15%",
    purchaseVatCode: "2",
    purchaseVatName: "IVA 15%",
    exciseTaxCode: null,
    exciseTaxName: null,
  },
  saleConfig: {
    isForSale: true,
    maxDiscountPercent: null,
    isAvailableOnWeb: false,
    isAvailableOnPOS: false,
    isAvailableOnMobile: false,
    isEcommerceActive: false,
    isFavorite: false,
  },
  stockConfig: {
    stockControlEnabled: true,
    tracksLot: false,
    tracksSeries: false,
    allowDecimalQty: false,
    allowDecimalSale: false,
    minStockQty: null,
    maxStockQty: null,
  },
  variants: [],
  images: [],
  unitConversions: [],
  substitutes: [],
  packagingLevels: [],
  supplierCodes: [],
};

export function renderModal(
  props: {
    open?: boolean;
    itemId?: string;
    initialData?: CreateItemInitialData;
    onClose?: () => void;
    onSaved?: (item: ItemCreatedResult) => void;
  } = {},
) {
  const onClose = props.onClose ?? vi.fn();
  const onSaved = props.onSaved ?? vi.fn();
  const utils = render(
    <I18nProvider>
      <ItemEditorModal
        open={props.open ?? true}
        itemId={props.itemId}
        initialData={props.initialData}
        onClose={onClose}
        onSaved={onSaved}
      />
    </I18nProvider>,
  );
  return { ...utils, onClose, onSaved };
}

export function fieldByName(container: HTMLElement, name: string): HTMLInputElement {
  const el = container.querySelector(`[name="${name}"]`);
  if (!el) throw new Error(`[name="${name}"] no encontrado`);
  return el as HTMLInputElement;
}

export async function waitForCatalogs() {
  await waitFor(() => expect(vi.mocked(apiGet)).toHaveBeenCalled());
}

export async function fillRequiredFields(
  container: HTMLElement,
  overrides: Partial<
    Record<"sku" | "barcode" | "saleVatCode" | "purchaseVatCode", string>
  > & { salePrice?: string } = {},
) {
  fireEvent.change(fieldByName(container, "sku"), {
    target: { value: overrides.sku ?? "SKU-1" },
  });
  fireEvent.change(fieldByName(container, "itemTypeId"), {
    target: { value: "type-1" },
  });
  fireEvent.change(fieldByName(container, "categoryNodeId"), {
    target: { value: "cat-1" },
  });
  fireEvent.change(fieldByName(container, "brandId"), {
    target: { value: "brand-1" },
  });
  fireEvent.change(fieldByName(container, "defaultUomCode"), {
    target: { value: "UNIT" },
  });
  if (overrides.barcode !== undefined) {
    fireEvent.change(fieldByName(container, "barcode"), {
      target: { value: overrides.barcode },
    });
  }
  fireEvent.change(fieldByName(container, "barcodeType"), {
    target: { value: "EAN13" },
  });
  // Obligatorios en modo Crear (fix "listo para comprar y vender sin reproceso manual"):
  // IVA de venta, IVA de compra y precio de venta.
  fireEvent.change(fieldByName(container, "saleVatCode"), {
    target: { value: overrides.saleVatCode ?? "2" },
  });
  fireEvent.change(fieldByName(container, "purchaseVatCode"), {
    target: { value: overrides.purchaseVatCode ?? "2" },
  });
  fireEvent.change(fieldByName(container, "salePrice"), {
    target: { value: overrides.salePrice ?? "10" },
  });
}
