// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { useState } from "react";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ExpenseSubcategorySelector } from "./ExpenseSubcategorySelector";
import type { ExpenseCategoryTreeNodeDto } from "../api/expenseCategoryService";
import type { AccountDto } from "../../accounting/facades/accountLookupFacade";

/**
 * EXPENSE-UX-01 (U3) — cambiar Tipo o Categoría nunca selecciona en silencio una subcategoría
 * distinta: se conserva si sigue siendo válida; si no, queda vacía y el usuario debe elegirla.
 */

function node(
  id: string,
  level: "Type" | "Category" | "Subcategory",
  children: ExpenseCategoryTreeNodeDto[] = [],
  accountId: string | null = null,
): ExpenseCategoryTreeNodeDto {
  return {
    id,
    companyId: "c-1",
    parentId: null,
    code: id.toUpperCase(),
    name: `Nombre ${id}`,
    description: null,
    level,
    accountingAccountId: accountId,
    isActive: true,
    children,
  };
}

const TREE: ExpenseCategoryTreeNodeDto[] = [
  node("t1", "Type", [
    node("c1", "Category", [
      node("s1", "Subcategory", [], "acc-1"),
      node("s2", "Subcategory", [], "acc-1"),
    ]),
    node("c2", "Category", [node("s3", "Subcategory", [], "acc-1")]),
  ]),
  node("t2", "Type", [node("c3", "Category", [node("s4", "Subcategory", [], "acc-1")])]),
];

const ACCOUNTS = new Map<string, AccountDto>([
  ["acc-1", { id: "acc-1", code: "5.1.1", name: "Servicios" } as AccountDto],
]);

afterEach(cleanup);

function Harness({ initial, spy }: Readonly<{ initial: string; spy?: (id: string) => void }>) {
  const [value, setValue] = useState(initial);
  return (
    <ExpenseSubcategorySelector
      tree={TREE}
      accountsById={ACCOUNTS}
      value={value}
      onChange={(next) => {
        spy?.(next);
        setValue(next);
      }}
    />
  );
}

const typeSelect = () => screen.getByLabelText(/^Tipo de gasto/) as HTMLSelectElement;
const categorySelect = () => screen.getByLabelText(/^Categoría/) as HTMLSelectElement;
const subSelect = () => screen.getByLabelText(/^Subcategoría/) as HTMLSelectElement;

function renderHarness(initial: string, spy?: (id: string) => void) {
  return render(<Harness initial={initial} spy={spy} />, { wrapper: I18nProvider });
}

describe("ExpenseSubcategorySelector — selección explícita (U3)", () => {
  it("cambiar el Tipo limpia la subcategoría y NO elige otra automáticamente", () => {
    const spy = vi.fn();
    renderHarness("s1", spy);
    expect(subSelect().value).toBe("s1");

    fireEvent.change(typeSelect(), { target: { value: "t2" } });

    expect(spy).toHaveBeenLastCalledWith("");
    expect(typeSelect().value).toBe("t2");
    expect(categorySelect().value).toBe("");
    expect(subSelect().value).toBe("");
    expect(subSelect().disabled).toBe(true);
  });

  it("tras cambiar el Tipo, el usuario elige categoría y subcategoría de forma explícita", () => {
    const spy = vi.fn();
    renderHarness("s1", spy);

    fireEvent.change(typeSelect(), { target: { value: "t2" } });
    fireEvent.change(categorySelect(), { target: { value: "c3" } });
    expect(categorySelect().value).toBe("c3");
    expect(subSelect().value).toBe("");
    expect(spy).toHaveBeenLastCalledWith("");

    fireEvent.change(subSelect(), { target: { value: "s4" } });
    expect(spy).toHaveBeenLastCalledWith("s4");
    expect(typeSelect().value).toBe("t2");
    expect(categorySelect().value).toBe("c3");
    expect(subSelect().value).toBe("s4");
  });

  it("cambiar la Categoría dentro del mismo Tipo limpia la subcategoría (no la reasigna)", () => {
    const spy = vi.fn();
    renderHarness("s1", spy);

    fireEvent.change(categorySelect(), { target: { value: "c2" } });

    expect(spy).toHaveBeenLastCalledWith("");
    expect(typeSelect().value).toBe("t1");
    expect(categorySelect().value).toBe("c2");
    expect(subSelect().value).toBe("");
  });

  it("volver a elegir el mismo Tipo o la misma Categoría conserva la subcategoría válida", () => {
    const spy = vi.fn();
    renderHarness("s2", spy);

    fireEvent.change(typeSelect(), { target: { value: "t1" } });
    fireEvent.change(categorySelect(), { target: { value: "c1" } });

    expect(spy).not.toHaveBeenCalled();
    expect(subSelect().value).toBe("s2");
  });

  it("sin subcategoría previa, elegir Tipo y Categoría no dispara ningún cambio de valor", () => {
    const spy = vi.fn();
    renderHarness("", spy);

    fireEvent.change(typeSelect(), { target: { value: "t1" } });
    fireEvent.change(categorySelect(), { target: { value: "c1" } });

    expect(spy).not.toHaveBeenCalled();
    expect(subSelect().disabled).toBe(false);
    expect(subSelect().value).toBe("");
  });

  it("dejar el Tipo en blanco limpia toda la selección", () => {
    const spy = vi.fn();
    renderHarness("s1", spy);

    fireEvent.change(typeSelect(), { target: { value: "" } });

    expect(spy).toHaveBeenLastCalledWith("");
    expect(typeSelect().value).toBe("");
    expect(categorySelect().value).toBe("");
    expect(subSelect().value).toBe("");
  });
});
