import { apiPost } from "../../lib/apiEnvelope";
import type { AddVariantRequest } from "./itemService";

export type ItemCodeKind = "Sku" | "VariantSku" | "Barcode" | "SupplierCode";
export interface ItemCodeCandidate {
  kind: ItemCodeKind;
  code: string;
  row: string;
  targetKey: string;
  supplierId?: string;
  excludeId?: string;
  variant?: AddVariantRequest & { itemId: string };
}
export interface ItemCodeConflict {
  kind: ItemCodeKind;
  code: string;
  itemId: string | null;
  itemName: string | null;
  rows: string[];
}

let revision = 0;
const listeners = new Set<() => void>();
export const codeConflictRevision = () => revision;
export function subscribeCodeConflicts(listener: () => void) {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}
export function refreshItemCodeConflicts() {
  revision++;
  listeners.forEach(listener => listener());
}
export async function itemCodeMutation<T>(operation: Promise<T>): Promise<T> {
  try { return await operation; }
  finally { refreshItemCodeConflicts(); }
}
export const getItemCodeConflicts = (codes: ItemCodeCandidate[]) =>
  apiPost<ItemCodeConflict[]>("/api/v1/items/code-conflicts", { codes });
