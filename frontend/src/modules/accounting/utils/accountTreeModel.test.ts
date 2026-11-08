import { describe, expect, it } from "vitest";
import type { AccountDto } from "../api/accountingApi";
import {
  ancestorIds,
  buildAccountTree,
  cleanAccountName,
  compareAccountCode,
  containerIds,
  expectedParentCode,
  filteredTreeRows,
  flatRows,
  initialCollapsedIds,
  visibleTreeRows,
} from "./accountTreeModel";

/** ACCOUNTING-CHART-UX-01 — modelo jerárquico puro del Plan de Cuentas. */

function acc(id: string, code: string, parentAccountId: string | null, allowsPosting = false): AccountDto {
  return {
    id,
    code,
    name: `Cuenta ${code}`,
    parentAccountId,
    parentAccountCode: null,
    parentAccountName: null,
    level: code.split(".").length - 1,
    accountType: "Asset",
    nature: "Debit",
    allowsPosting,
    isActive: true,
    createdAt: "2026-08-01T00:00:00Z",
    updatedAt: null,
  };
}

// Entregado desordenado a propósito: el modelo ordena por código natural.
const ACCOUNTS = [
  acc("c10", "1.10", "a1"),
  acc("leaf2", "1.1.02", "c11", true),
  acc("a1", "1", null),
  acc("c11", "1.1", "a1"),
  acc("leaf1", "1.1.01", "c11", true),
  acc("b2", "2", null),
  acc("c2", "1.2", "a1"),
];

const codes = (rows: { account: AccountDto }[]) => rows.map((r) => r.account.code);

describe("accountTreeModel", () => {
  it("ordena por código natural (1.2 antes que 1.10)", () => {
    expect(compareAccountCode("1.2", "1.10")).toBeLessThan(0);
    expect(compareAccountCode("1.1", "1.1.01")).toBeLessThan(0);
    expect(compareAccountCode("2", "1.10")).toBeGreaterThan(0);
  });

  it("construye la jerarquía con parentAccountId y calcula profundidad", () => {
    const tree = buildAccountTree(ACCOUNTS);
    expect(tree.roots.map((a) => a.code)).toEqual(["1", "2"]);
    expect(tree.childrenById.get("a1")?.map((a) => a.code)).toEqual(["1.1", "1.2", "1.10"]);
    expect(tree.depthById.get("leaf1")).toBe(2);
    expect(tree.orphanIds.size).toBe(0);
  });

  it("trata como raíz (huérfana) la cuenta con padre ausente, propio o en ciclo, sin perderla", () => {
    const tree = buildAccountTree([
      acc("x", "9.1", "missing"),
      acc("self", "8", "self"),
      acc("p", "7.1", "q"),
      acc("q", "7.2", "p"),
    ]);
    expect(tree.roots.map((a) => a.code).sort()).toEqual(["7.1", "7.2", "8", "9.1"]);
    expect([...tree.orphanIds].sort()).toEqual(["p", "q", "self", "x"]);
    expect(visibleTreeRows(tree, new Set())).toHaveLength(4);
    expect(ancestorIds(tree, "x")).toEqual([]);
  });

  it("estado inicial: niveles 0 y 1 visibles (se contraen contenedores desde nivel 1)", () => {
    const tree = buildAccountTree(ACCOUNTS);
    const collapsed = initialCollapsedIds(tree);
    expect([...collapsed]).toEqual(["c11"]);
    expect(codes(visibleTreeRows(tree, collapsed))).toEqual(["1", "1.1", "1.2", "1.10", "2"]);
  });

  it("expandir todo / contraer todo", () => {
    const tree = buildAccountTree(ACCOUNTS);
    expect(codes(visibleTreeRows(tree, new Set()))).toEqual([
      "1", "1.1", "1.1.01", "1.1.02", "1.2", "1.10", "2",
    ]);
    expect(codes(visibleTreeRows(tree, new Set(containerIds(tree))))).toEqual(["1", "2"]);
  });

  it("vista filtrada: coincidencias + ancestros de contexto, ignorando la contracción", () => {
    const tree = buildAccountTree(ACCOUNTS);
    const rows = filteredTreeRows(tree, new Set(["leaf2", "b2"]));
    expect(codes(rows)).toEqual(["1", "1.1", "1.1.02", "2"]);
    expect(rows.map((r) => r.isContext)).toEqual([true, true, false, false]);
    expect(rows.map((r) => r.depth)).toEqual([0, 1, 2, 0]);
  });

  it("un ancestro que también coincide no se marca como contexto", () => {
    const tree = buildAccountTree(ACCOUNTS);
    const rows = filteredTreeRows(tree, new Set(["c11", "leaf1"]));
    expect(codes(rows)).toEqual(["1", "1.1", "1.1.01"]);
    expect(rows.map((r) => r.isContext)).toEqual([true, false, false]);
  });

  it("vista plana: solo coincidencias, ordenadas y sin sangría", () => {
    const tree = buildAccountTree(ACCOUNTS);
    const rows = flatRows(tree, new Set(["leaf2", "leaf1", "c2"]));
    expect(codes(rows)).toEqual(["1.1.01", "1.1.02", "1.2"]);
    expect(rows.every((r) => r.depth === 0 && !r.isContext)).toBe(true);
  });

  it("ancestorIds devuelve la ruta desde la raíz", () => {
    expect(ancestorIds(buildAccountTree(ACCOUNTS), "leaf1")).toEqual(["a1", "c11"]);
  });

  it("expectedParentCode replica la regla canónica del backend", () => {
    expect(expectedParentCode("1.1.01")).toBe("1.1");
    expect(expectedParentCode("1.1.")).toBe("1.1");
    expect(expectedParentCode("1")).toBeNull();
    expect(expectedParentCode("")).toBeNull();
  });

  it("cleanAccountName quita prefijos de árbol heredados", () => {
    expect(cleanAccountName("L Caja chica")).toBe("Caja chica");
    expect(cleanAccountName("│ ├ Bancos")).toBe("Bancos");
    expect(cleanAccountName("Lotes")).toBe("Lotes");
  });
});
