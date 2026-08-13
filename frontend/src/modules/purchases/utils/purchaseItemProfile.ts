import { normalizeOptionalCode } from "../../../lib/sanitizers";
import type { ItemDto } from "../../../types/items";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";

type PurchaseItemSource = Pick<ItemDto, "id" | "sku" | "shortName"> &
  Partial<Pick<ItemDto, "description" | "baseSalePrice">> & {
    name?: string | null;
    purchaseVatCode?: string | null;
    exciseTaxCode?: string | null;
    taxConfig?: {
      purchaseVatCode?: string | null;
      exciseTaxCode?: string | null;
    } | null;
    stockConfig?: {
      minStockQty?: number | null;
    } | null;
  };

export type ProductProfile = {
  id: string;
  sku: string;
  name: string;
  description: string;
  label: string;
  purchaseVatCode: string | null;
  appliesExciseTax: boolean;
  exciseTaxCode: string | null;
  minStockQty: number | null;
  currentPvp: number;
  lastCost?: number;
  vatRate?: string;
};

type PresentationSource = Pick<
  Partial<PurchaseLineFormValues>,
  | "packagingLevelId"
  | "uomCode"
  | "baseUomCode"
  | "conversionFactor"
  | "quantityInBaseUom"
>;

export function buildPurchaseItemLabel(input: {
  sku: string;
  name?: string | null;
  shortName?: string | null;
}) {
  return `${input.sku} \u2014 ${input.name ?? input.shortName ?? ""}`;
}

export function buildPurchaseItemProfile(
  item: PurchaseItemSource,
  options?: { vatRates?: Record<string, number> },
): ProductProfile {
  const purchaseVatCode =
    item.taxConfig?.purchaseVatCode ?? item.purchaseVatCode ?? null;
  const exciseTaxCode =
    normalizeOptionalCode(item.taxConfig?.exciseTaxCode ?? item.exciseTaxCode) ??
    null;
  const name = item.name ?? item.shortName;
  const vatPct = purchaseVatCode
    ? options?.vatRates?.[purchaseVatCode]
    : undefined;

  return {
    id: item.id,
    sku: item.sku,
    name,
    description: item.description ?? "",
    label: buildPurchaseItemLabel({ sku: item.sku, name }),
    purchaseVatCode,
    appliesExciseTax: exciseTaxCode != null,
    exciseTaxCode,
    minStockQty: item.stockConfig?.minStockQty ?? null,
    currentPvp: item.baseSalePrice ?? 0,
    vatRate: vatPct !== undefined ? `${vatPct}%` : undefined,
  };
}

export function normalizePurchaseLinePresentation(
  source?: PresentationSource | null,
): PresentationSource {
  return {
    packagingLevelId: source?.packagingLevelId ?? undefined,
    uomCode: source?.uomCode ?? undefined,
    baseUomCode: source?.baseUomCode ?? undefined,
    conversionFactor: source?.conversionFactor,
    quantityInBaseUom: source?.quantityInBaseUom,
  };
}

export function resolvePurchaseItemSelection(
  profile: ProductProfile,
  options?: {
    existingLine?: PurchaseLineFormValues | null;
    overwriteVatCode?: boolean;
  },
): Partial<PurchaseLineFormValues> {
  const existingLine = options?.existingLine;
  const shouldApplyVat = options?.overwriteVatCode || !existingLine?.vatCode;
  const patch: Partial<PurchaseLineFormValues> = {
    itemId: profile.id,
    description: profile.label,
    ...normalizePurchaseLinePresentation(existingLine),
  };

  if (shouldApplyVat) {
    patch.vatCode = profile.purchaseVatCode ?? "";
  }
  if (profile.exciseTaxCode) {
    patch.iceCode = profile.exciseTaxCode;
  }

  return patch;
}

export function buildPurchaseLineFromItem(
  item: PurchaseItemSource,
  options: {
    key: number;
    quantity?: number;
    unitPrice?: number;
    discountPct?: number;
    vatRates?: Record<string, number>;
    presentation?: PresentationSource | null;
  },
): PurchaseLineFormValues {
  const profile = buildPurchaseItemProfile(item, {
    vatRates: options.vatRates,
  });

  return {
    _key: options.key,
    itemId: profile.id,
    description: profile.label,
    quantity: options.quantity ?? 1,
    unitPrice: options.unitPrice ?? 0,
    vatCode: profile.purchaseVatCode ?? "",
    discountPct: options.discountPct ?? 0,
    iceCode: normalizeOptionalCode(profile.exciseTaxCode) ?? undefined,
    ...normalizePurchaseLinePresentation(options.presentation),
  };
}
