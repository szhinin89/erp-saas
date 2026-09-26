// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { ZhSearchSelect } from "./ZhSearchSelect";
import {
  buildSearchIndex,
  filterSearchIndex,
  normalizeSearchText,
} from "./searchSelectFilter";

/** ZH-SUPPLIER-SEARCH-REUSABLE-01 — autocomplete genérico del DS (sin dominio). */

interface Opt {
  ruc: string;
  name: string;
}

const getKey = (o: Opt) => o.ruc;
const getLabel = (o: Opt) => o.name;
const getText = (o: Opt) => `${o.name} ${o.ruc}`;

const SUPPLIERS: Opt[] = [
  { ruc: "0990789061001", name: "DISTRIBUIDORA IMPORTADORA DIPOR S.A." },
  { ruc: "1790012345001", name: "Comercial Ñandú Cía. Ltda." },
  { ruc: "0999999999001", name: "Proveedor No Registrado" },
];

function manySuppliers(n: number): Opt[] {
  return Array.from({ length: n }, (_, i) => ({
    ruc: String(1_000_000_000_001 + i),
    name: `Proveedor ${String(i).padStart(4, "0")}`,
  }));
}

function MultipleHarness({
  options,
  maxResults,
  onChangeSpy,
}: {
  options: readonly Opt[];
  maxResults?: number;
  onChangeSpy?: (v: Opt[]) => void;
}) {
  const [value, setValue] = useState<Opt[]>([]);
  return (
    <ZhSearchSelect
      mode="multiple"
      options={options}
      value={value}
      onChange={(v) => {
        setValue(v);
        onChangeSpy?.(v);
      }}
      getOptionKey={getKey}
      getOptionLabel={getLabel}
      getOptionDescription={getKey}
      getOptionSearchText={getText}
      getChipLabel={(o) => `${o.name} — ${o.ruc}`}
      maxResults={maxResults}
      aria-label="Proveedor"
      removeLabel="Quitar"
    />
  );
}

function SingleHarness({ onChangeSpy }: { onChangeSpy?: (v: Opt | null) => void }) {
  const [value, setValue] = useState<Opt | null>(null);
  return (
    <ZhSearchSelect
      options={SUPPLIERS}
      value={value}
      onChange={(v) => {
        setValue(v);
        onChangeSpy?.(v);
      }}
      getOptionKey={getKey}
      getOptionLabel={getLabel}
      getOptionDescription={getKey}
      getOptionSearchText={getText}
      aria-label="Proveedor"
      clearLabel="Cambiar"
    />
  );
}

const input = () => screen.getByRole("combobox");
const optionsShown = () => screen.queryAllByRole("option");
const type = (text: string) => fireEvent.change(input(), { target: { value: text } });

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("searchSelectFilter (helpers puros)", () => {
  it("normaliza mayúsculas, tildes y espacios", () => {
    expect(normalizeSearchText("  Ñandú   CÍA ")).toBe("nandu cia");
  });

  it("corta en maxResults y excluye claves", () => {
    const index = buildSearchIndex(manySuppliers(100), getKey, getText);
    const out = filterSearchIndex(index, "proveedor", 30, new Set([index[0].key]));
    expect(out).toHaveLength(30);
    expect(out[0].name).toBe("Proveedor 0001");
  });
});

describe("ZhSearchSelect — datasource local, modo múltiple", () => {
  it("busca por nombre", () => {
    render(<MultipleHarness options={SUPPLIERS} />);
    type("dipo");
    expect(optionsShown()).toHaveLength(1);
    expect(screen.getByText("DISTRIBUIDORA IMPORTADORA DIPOR S.A.")).toBeTruthy();
    expect(screen.getByText("0990789061001")).toBeTruthy();
  });

  it("busca por RUC", () => {
    render(<MultipleHarness options={SUPPLIERS} />);
    type("179001");
    expect(optionsShown()).toHaveLength(1);
    expect(screen.getByText("Comercial Ñandú Cía. Ltda.")).toBeTruthy();
  });

  it("es case/accent insensitive", () => {
    render(<MultipleHarness options={SUPPLIERS} />);
    type("NANDU");
    expect(optionsShown()).toHaveLength(1);
    type("DiPoR");
    expect(optionsShown()).toHaveLength(1);
  });

  it("muestra empty state cuando no hay coincidencias", () => {
    render(<MultipleHarness options={SUPPLIERS} />);
    type("zzz");
    expect(optionsShown()).toHaveLength(0);
    expect(screen.getByText("Sin resultados")).toBeTruthy();
  });

  it("selecciona varios como chips, excluye los ya seleccionados y permite quitar un chip", () => {
    const spy = vi.fn();
    render(<MultipleHarness options={SUPPLIERS} onChangeSpy={spy} />);
    type("dipo");
    fireEvent.click(screen.getByText("DISTRIBUIDORA IMPORTADORA DIPOR S.A."));
    type("registrado");
    fireEvent.click(screen.getByText("Proveedor No Registrado"));

    expect(spy).toHaveBeenLastCalledWith([SUPPLIERS[0], SUPPLIERS[2]]);
    expect(
      screen.getByRole("button", {
        name: "Quitar DISTRIBUIDORA IMPORTADORA DIPOR S.A. — 0990789061001",
      }),
    ).toBeTruthy();

    // Ya seleccionado → no vuelve a aparecer en resultados.
    type("dipo");
    expect(optionsShown()).toHaveLength(0);

    fireEvent.click(
      screen.getByRole("button", {
        name: "Quitar DISTRIBUIDORA IMPORTADORA DIPOR S.A. — 0990789061001",
      }),
    );
    expect(spy).toHaveBeenLastCalledWith([SUPPLIERS[2]]);
  });

  it("clear deja la selección vacía (= todos)", () => {
    const spy = vi.fn();
    render(<MultipleHarness options={SUPPLIERS} onChangeSpy={spy} />);
    type("dipo");
    fireEvent.click(screen.getByText("DISTRIBUIDORA IMPORTADORA DIPOR S.A."));
    fireEvent.click(screen.getByRole("button", { name: "Limpiar selección" }));
    expect(spy).toHaveBeenLastCalledWith([]);
  });

  it("teclado: flechas + Enter selecciona, Backspace con input vacío quita el último chip, Escape cierra", () => {
    const spy = vi.fn();
    render(<MultipleHarness options={SUPPLIERS} onChangeSpy={spy} />);
    type("s.a");
    fireEvent.keyDown(input(), { key: "ArrowDown" });
    expect(input().getAttribute("aria-activedescendant")).toBeTruthy();
    fireEvent.keyDown(input(), { key: "Enter" });
    expect(spy).toHaveBeenLastCalledWith([SUPPLIERS[0]]);

    fireEvent.keyDown(input(), { key: "Backspace" });
    expect(spy).toHaveBeenLastCalledWith([]);

    type("dipo");
    expect(input().getAttribute("aria-expanded")).toBe("true");
    fireEvent.keyDown(input(), { key: "Escape" });
    expect(input().getAttribute("aria-expanded")).toBe("false");
    expect(optionsShown()).toHaveLength(0);
  });

  it("respeta el máximo configurable de resultados", () => {
    render(<MultipleHarness options={manySuppliers(50)} maxResults={5} />);
    type("proveedor");
    expect(optionsShown()).toHaveLength(5);
    expect(screen.getByText("Siga escribiendo para refinar la búsqueda.")).toBeTruthy();
  });

  it("con 2.000 opciones nunca renderiza más de 30 (default) y filtra conforme escribe", () => {
    render(<MultipleHarness options={manySuppliers(2000)} />);
    fireEvent.focus(input());
    expect(optionsShown()).toHaveLength(30);
    type("proveedor 19");
    expect(optionsShown()).toHaveLength(30);
    type("proveedor 1999");
    expect(optionsShown()).toHaveLength(1);
  });
});

describe("ZhSearchSelect — modo single", () => {
  it("selecciona uno, muestra la tarjeta seleccionada y permite limpiar", () => {
    const spy = vi.fn();
    render(<SingleHarness onChangeSpy={spy} />);
    type("dipo");
    fireEvent.click(screen.getByText("DISTRIBUIDORA IMPORTADORA DIPOR S.A."));
    expect(spy).toHaveBeenLastCalledWith(SUPPLIERS[0]);
    expect(screen.queryByRole("combobox")).toBeNull();
    expect(screen.getByText("0990789061001")).toBeTruthy();

    fireEvent.click(screen.getByTitle("Cambiar"));
    expect(spy).toHaveBeenLastCalledWith(null);
    expect(screen.getByRole("combobox")).toBeTruthy();
  });
});

describe("ZhSearchSelect — datasource remoto", () => {
  it("debounce, loading, cancela la búsqueda anterior y no busca bajo el mínimo", async () => {
    vi.useFakeTimers();
    const signals: AbortSignal[] = [];
    let resolveLast: (rows: Opt[]) => void = () => {};
    const loadOptions = vi.fn(
      (_q: string, signal: AbortSignal) =>
        new Promise<Opt[]>((resolve) => {
          signals.push(signal);
          resolveLast = resolve;
        }),
    );
    render(
      <ZhSearchSelect
        options={undefined}
        loadOptions={loadOptions}
        value={null}
        onChange={() => {}}
        getOptionKey={getKey}
        getOptionLabel={getLabel}
        getOptionDescription={getKey}
      />,
    );

    type("d");
    act(() => vi.advanceTimersByTime(400));
    expect(loadOptions).not.toHaveBeenCalled();

    type("di");
    act(() => vi.advanceTimersByTime(299));
    expect(loadOptions).not.toHaveBeenCalled();
    act(() => vi.advanceTimersByTime(1));
    expect(loadOptions).toHaveBeenCalledTimes(1);
    expect(loadOptions).toHaveBeenLastCalledWith("di", expect.any(AbortSignal));
    expect(screen.getAllByText("Buscando...").length).toBeGreaterThan(0);

    type("dip");
    expect(signals[0].aborted).toBe(true);
    act(() => vi.advanceTimersByTime(300));
    expect(loadOptions).toHaveBeenCalledTimes(2);

    await act(async () => {
      resolveLast([SUPPLIERS[0]]);
    });
    expect(optionsShown()).toHaveLength(1);
    expect(screen.queryByText("Buscando...")).toBeNull();
  });
});
