import type { AccountDto } from "../api/accountingApi";

/** ACCOUNTING-CHART-UX-01 — fixtures compartidas por las pruebas del Plan de cuentas. */

export function account(overrides: Partial<AccountDto> & Pick<AccountDto, "id" | "code" | "name">): AccountDto {
  return {
    parentAccountId: null,
    parentAccountCode: null,
    parentAccountName: null,
    level: overrides.code.split(".").length - 1,
    accountType: "Asset",
    nature: "Debit",
    allowsPosting: true,
    isActive: true,
    createdAt: "2026-08-01T00:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

const parentOf = (id: string, code: string, name: string) => ({
  parentAccountId: id,
  parentAccountCode: code,
  parentAccountName: name,
});

/**
 * Árbol: 1 Activo › 1.1 Activo corriente › {1.1.01 Caja general, 1.1.02 Bancos (inactiva)};
 * 2 Pasivo › 2.1 Proveedores. Agrupadoras: 1, 1.1, 2. Movimiento: 1.1.01, 1.1.02, 2.1.
 * Entregado desordenado a propósito.
 */
export const CHART_ACCOUNTS: AccountDto[] = [
  account({ id: "leaf2", code: "1.1.02", name: "Bancos", isActive: false, ...parentOf("c11", "1.1", "Activo corriente") }),
  account({ id: "a1", code: "1", name: "Activo", allowsPosting: false }),
  account({ id: "c21", code: "2.1", name: "Proveedores", accountType: "Liability", nature: "Credit", ...parentOf("b2", "2", "Pasivo") }),
  account({ id: "c11", code: "1.1", name: "Activo corriente", allowsPosting: false, ...parentOf("a1", "1", "Activo") }),
  account({ id: "leaf1", code: "1.1.01", name: "Caja general", ...parentOf("c11", "1.1", "Activo corriente") }),
  account({ id: "b2", code: "2", name: "Pasivo", accountType: "Liability", nature: "Credit", allowsPosting: false }),
];

export const byId = (id: string) => CHART_ACCOUNTS.find((a) => a.id === id)!;

/** Códigos visibles en el orden de la tabla. */
export function visibleCodes(container: HTMLElement): string[] {
  return Array.from(container.querySelectorAll("tbody tr .zh-account-ref__code")).map(
    (el) => el.textContent ?? "",
  );
}
