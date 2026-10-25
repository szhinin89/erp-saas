import type { ItemPackagingLevelDto } from "../../../../types/items";

export type PackagingLevelInput = {
  id?: string | null;
  name: string;
  level: number;
  baseQuantity: number;
  uomCode: string;
  barcode?: string | null;
  weight?: number | null;
  isBaseUnit?: boolean;
  isPurchaseDefault?: boolean;
  isSaleDefault?: boolean;
};

export type PackagingLevelDraft = {
  name: string;
  baseQuantity: string;
  uomCode: string;
  barcode: string;
  weight: string;
  isBaseUnit: boolean;
  isPurchaseDefault: boolean;
  isSaleDefault: boolean;
};

export const emptyPackagingDraft: PackagingLevelDraft = {
  name: "",
  baseQuantity: "",
  uomCode: "",
  barcode: "",
  weight: "",
  isBaseUnit: false,
  isPurchaseDefault: false,
  isSaleDefault: false,
};

export function toPackagingDraft(level: ItemPackagingLevelDto): PackagingLevelDraft {
  return {
    name: level.name,
    baseQuantity: String(level.baseQuantity),
    uomCode: level.uomCode,
    barcode: level.barcode ?? "",
    weight: level.weight != null ? String(level.weight) : "",
    isBaseUnit: level.isBaseUnit,
    isPurchaseDefault: level.isPurchaseDefault,
    isSaleDefault: level.isSaleDefault,
  };
}

export function packagingNameSuggestsMultiplier(name: string, baseQuantity: string) {
  const qty = Number(baseQuantity);
  return qty === 1 && /\b[xX]\s*(?:[2-9]|\d{2,})\b/.test(name);
}

export function buildPackagingPayload(
  rows: { id: string | null; draft: PackagingLevelDraft }[],
): PackagingLevelInput[] {
  return rows.map((r, index) => ({
    id: r.id && !r.id.startsWith("__") ? r.id : null,
    name: r.draft.name.trim(),
    level: index + 1,
    baseQuantity: Number(r.draft.baseQuantity),
    uomCode: r.draft.uomCode,
    barcode: r.draft.barcode.trim() ? r.draft.barcode.trim() : null,
    weight: r.draft.weight.trim() ? Number(r.draft.weight) : null,
    isBaseUnit: r.draft.isBaseUnit,
    isPurchaseDefault: r.draft.isPurchaseDefault,
    isSaleDefault: r.draft.isSaleDefault,
  }));
}

export function getErrorMessage(error: unknown, fallback: string): string {
  return error instanceof Error && error.message.trim()
    ? error.message.trim()
    : fallback;
}

/** Fuerza que a lo sumo una fila quede marcada como unidad base, igual que exige el backend. */
export function withBaseUnitExclusivity(
  rows: { id: string | null; draft: PackagingLevelDraft }[],
  keepId: string | null,
): { id: string | null; draft: PackagingLevelDraft }[] {
  const keep = rows.find((r) => r.id === keepId);
  if (!keep?.draft.isBaseUnit) return rows;
  return rows.map((r) =>
    r.id === keepId ? r : { ...r, draft: { ...r.draft, isBaseUnit: false } },
  );
}
