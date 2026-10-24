export interface PurchaseFieldIssue { path: string; message: string }

/** Read RHF/Zod/API field trees without interpreting or changing their validation rules. */
export function purchaseFieldErrors(value: unknown, path = ""): PurchaseFieldIssue[] {
  if (!value || typeof value !== "object") return [];
  const record = value as Record<string, unknown>;
  if (typeof record.message === "string" && record.message.trim()) return [{ path, message: record.message }];
  return Object.entries(record).flatMap(([field, child]) =>
    ["ref", "type", "types"].includes(field) ? [] : purchaseFieldErrors(child, path ? `${path}.${field}` : field));
}
