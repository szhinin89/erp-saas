import { describe, expect, it } from "vitest";
import type { ExpenseCategoryTreeNodeDto } from "../api/expenseCategoryService";
import type { ExpenseDraftLineState } from "../components/ExpenseDocumentLinesEditor";
import {
  filterExpenseTreeByScope,
  lineScopeIssue,
  reconcileLinesWithScope,
  scopeFromDto,
  type ExpenseSupplierScope,
} from "./expenseSupplierScopeModel";

const line = (
  over: Partial<ExpenseDraftLineState> = {},
): ExpenseDraftLineState => ({
  key: Math.random().toString(),
  expenseSubcategoryId: "",
  description: "",
  quantity: "1",
  unitPrice: "0",
  discountValue: "0",
  vatCode: "0",
  notes: "",
  ...over,
});
const dto = (
  mode: "Unconfigured" | "Restricted" | "RestrictedEmpty",
  ids: string[] = [],
) => scopeFromDto("sup", { mode, items: [], availableSubcategoryIds: ids });
const persisted = (id: string) =>
  line({ expenseSubcategoryId: id, persistedSubcategoryId: id });

describe("expenseSupplierScopeModel", () => {
  it("modos: Unconfigured no restringe, Restricted limita, RestrictedEmpty no permite nada", () => {
    expect(dto("Unconfigured")).toMatchObject({
      allowedIds: null,
      singleId: null,
    });
    expect(dto("Restricted", ["a", "b"])).toMatchObject({ singleId: null });
    expect(dto("Restricted", ["a"])).toMatchObject({ singleId: "a" });
    const empty = dto("RestrictedEmpty");
    expect(empty).toMatchObject({ singleId: null });
    expect(
      lineScopeIssue(line({ expenseSubcategoryId: "x" }), empty, false),
    ).toBe("blocked");
  });

  it("reconcile: limpia la nueva incompatible y asigna la única permitida; no toca lo persistido", () => {
    const scope = dto("Restricted", ["ok"]);
    const saved = persisted("old");
    const { lines, cleared } = reconcileLinesWithScope(
      [saved, line({ expenseSubcategoryId: "bad" }), line()],
      scope,
    );
    expect(lines[0]).toBe(saved);
    expect(lines[1].expenseSubcategoryId).toBe("ok");
    expect(lines[2].expenseSubcategoryId).toBe("ok");
    expect(cleared).toBe(1);
  });

  it("reconcile: con varias permitidas deja vacías las líneas vacías y conserva las válidas", () => {
    const scope = dto("Restricted", ["a", "b"]);
    const valid = line({ expenseSubcategoryId: "a" });
    const empty = line();
    const result = reconcileLinesWithScope([valid, empty], scope);
    expect(result.lines[0]).toBe(valid);
    expect(result.lines[1].expenseSubcategoryId).toBe("");
    expect(result.cleared).toBe(0);
  });

  it("reconcile: devuelve la misma lista si nada cambia o si el alcance no está listo", () => {
    const lines = [line({ expenseSubcategoryId: "a" })];
    expect(
      reconcileLinesWithScope(lines, dto("Restricted", ["a", "b"])).lines,
    ).toBe(lines);
    expect(reconcileLinesWithScope(lines, dto("Unconfigured")).lines).toBe(
      lines,
    );
    const loading: ExpenseSupplierScope = { status: "loading" };
    expect(reconcileLinesWithScope(lines, loading).lines).toBe(lines);
  });

  it("incidencias: persistida + mismo proveedor = solo aviso; con proveedor cambiado o selección nueva = bloqueo", () => {
    const scope = dto("Restricted", ["ok"]);
    expect(lineScopeIssue(persisted("old"), scope, false)).toBe("saved");
    expect(lineScopeIssue(persisted("old"), scope, true)).toBe("blocked");
    expect(
      lineScopeIssue(line({ expenseSubcategoryId: "old" }), scope, false),
    ).toBe("blocked");
    expect(lineScopeIssue(persisted("ok"), scope, true)).toBeNull();
    expect(lineScopeIssue(line(), scope, true)).toBeNull();
    // una persistida cambiada en esta sesión deja de contar como persistida
    expect(
      lineScopeIssue(
        line({ expenseSubcategoryId: "x", persistedSubcategoryId: "old" }),
        scope,
        false,
      ),
    ).toBe("blocked");
  });

  it("árbol: solo ramas con subcategorías permitidas más la actual de la línea", () => {
    const sub = (id: string): ExpenseCategoryTreeNodeDto =>
      ({ id, children: [] }) as unknown as ExpenseCategoryTreeNodeDto;
    const branch = (
      id: string,
      children: ExpenseCategoryTreeNodeDto[],
    ): ExpenseCategoryTreeNodeDto =>
      ({ id, children }) as unknown as ExpenseCategoryTreeNodeDto;
    const tree = [
      branch("t1", [branch("c1", [sub("s1"), sub("s2")])]),
      branch("t2", [branch("c2", [sub("s3")])]),
    ];
    const ids = (nodes: ExpenseCategoryTreeNodeDto[]) =>
      nodes.flatMap((t) =>
        t.children.flatMap((c) => c.children.map((s) => s.id)),
      );
    expect(
      ids(filterExpenseTreeByScope(tree, dto("Restricted", ["s1"]), "")),
    ).toEqual(["s1"]);
    expect(
      ids(filterExpenseTreeByScope(tree, dto("Restricted", ["s1"]), "s3")),
    ).toEqual(["s1", "s3"]);
    expect(filterExpenseTreeByScope(tree, dto("RestrictedEmpty"), "")).toEqual(
      [],
    );
    expect(filterExpenseTreeByScope(tree, dto("Unconfigured"), "")).toBe(tree);
  });
});
