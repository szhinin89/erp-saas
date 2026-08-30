// @vitest-environment jsdom
import type { ReactElement } from "react";
import { describe, expect, it } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import { afterEach } from "vitest";
import { I18nProvider } from "../../i18n/i18n";
import { ZHDataTable, type ZHDataTableColumn } from "./ZHDataTable";

/**
 * ZH-DATATABLE-ROW-NUMBER-01 — la columna "N°" es opt-in (`showRowNumber`), es solo una
 * referencia visual de fila (nunca el Id) y no debe alterar el render de los consumidores
 * existentes que no la activan.
 */

interface Row {
  id: string;
  code: string;
  name: string;
}

const ROWS: Row[] = [
  { id: "id-1", code: "A-001", name: "Primero" },
  { id: "id-2", code: "A-002", name: "Segundo" },
  { id: "id-3", code: "A-003", name: "Tercero" },
];

const COLUMNS: ZHDataTableColumn<Row>[] = [
  { key: "code", header: "Código", render: (row) => row.code },
  { key: "name", header: "Nombre", render: (row) => row.name },
];

function renderTable(ui: ReactElement) {
  return render(<I18nProvider>{ui}</I18nProvider>);
}

afterEach(() => cleanup());

describe("ZHDataTable — showRowNumber", () => {
  it("sin showRowNumber, el render es igual que antes (sin columna N°)", () => {
    renderTable(<ZHDataTable columns={COLUMNS} rows={ROWS} rowKey={(r) => r.id} />);

    expect(screen.queryByText("N°")).toBeFalsy();
    const headers = screen.getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers).toEqual(["Código", "Nombre"]);
  });

  it("con showRowNumber, aparece la columna N° antes de las columnas de datos", () => {
    renderTable(
      <ZHDataTable columns={COLUMNS} rows={ROWS} rowKey={(r) => r.id} showRowNumber />,
    );

    const headers = screen.getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers).toEqual(["N°", "Código", "Nombre"]);
  });

  it("los números inician en 1 si no hay rowNumberOffset", () => {
    renderTable(
      <ZHDataTable columns={COLUMNS} rows={ROWS} rowKey={(r) => r.id} showRowNumber />,
    );

    const rows = screen.getAllByRole("row").slice(1); // sin la fila de encabezado
    const firstCellValues = rows.map((row) => within(row).getAllByRole("cell")[0].textContent);
    expect(firstCellValues).toEqual(["1", "2", "3"]);
  });

  it("respeta rowNumberOffset", () => {
    renderTable(
      <ZHDataTable
        columns={COLUMNS}
        rows={ROWS}
        rowKey={(r) => r.id}
        showRowNumber
        rowNumberOffset={10}
      />,
    );

    const rows = screen.getAllByRole("row").slice(1);
    const firstCellValues = rows.map((row) => within(row).getAllByRole("cell")[0].textContent);
    expect(firstCellValues).toEqual(["11", "12", "13"]);
  });

  it("no muestra el Id del registro en la columna N°", () => {
    renderTable(
      <ZHDataTable columns={COLUMNS} rows={ROWS} rowKey={(r) => r.id} showRowNumber />,
    );

    const rows = screen.getAllByRole("row").slice(1);
    const firstCellValues = rows.map((row) => within(row).getAllByRole("cell")[0].textContent);
    expect(firstCellValues).not.toContain("id-1");
    expect(firstCellValues).not.toContain("id-2");
    expect(firstCellValues).not.toContain("id-3");
  });

  it("LoadingState se muestra igual con o sin showRowNumber", () => {
    const { rerender } = renderTable(
      <ZHDataTable columns={COLUMNS} rows={ROWS} rowKey={(r) => r.id} loading />,
    );
    expect(screen.queryByRole("table")).toBeFalsy();

    rerender(
      <I18nProvider>
        <ZHDataTable columns={COLUMNS} rows={ROWS} rowKey={(r) => r.id} loading showRowNumber />
      </I18nProvider>,
    );
    expect(screen.queryByRole("table")).toBeFalsy();
  });

  it("EmptyState se muestra igual con o sin showRowNumber", () => {
    const { rerender } = renderTable(
      <ZHDataTable columns={COLUMNS} rows={[]} rowKey={(r) => r.id} emptyMessage="Sin datos." />,
    );
    expect(screen.getByText("Sin datos.")).toBeTruthy();
    expect(screen.queryByRole("table")).toBeFalsy();

    rerender(
      <I18nProvider>
        <ZHDataTable
          columns={COLUMNS}
          rows={[]}
          rowKey={(r) => r.id}
          emptyMessage="Sin datos."
          showRowNumber
        />
      </I18nProvider>,
    );
    expect(screen.getByText("Sin datos.")).toBeTruthy();
    expect(screen.queryByRole("table")).toBeFalsy();
  });
});

describe("ZHDataTable — rowClassName (ZH-LISTING-MIGRATION-ALL-02)", () => {
  it("sin rowClassName, las filas no tienen clase adicional", () => {
    renderTable(<ZHDataTable columns={COLUMNS} rows={ROWS} rowKey={(r) => r.id} />);
    const rows = screen.getAllByRole("row").slice(1);
    expect(rows.every((r) => r.className === "")).toBe(true);
  });

  it("con rowClassName, aplica la clase devuelta por fila", () => {
    renderTable(
      <ZHDataTable
        columns={COLUMNS}
        rows={ROWS}
        rowKey={(r) => r.id}
        rowClassName={(row) => (row.id === "id-2" ? "my-highlight" : undefined)}
      />,
    );
    const rows = screen.getAllByRole("row").slice(1);
    expect(rows[0].className).toBe("");
    expect(rows[1].className).toBe("my-highlight");
    expect(rows[2].className).toBe("");
  });
});

describe("ZHDataTable — tableClassName (ZH-LISTING-GLOBAL-STANDARD-06)", () => {
  it("sin tableClassName, la tabla solo tiene la clase base", () => {
    renderTable(<ZHDataTable columns={COLUMNS} rows={ROWS} rowKey={(r) => r.id} />);
    expect(screen.getByRole("table").className).toBe("table");
  });

  it("con tableClassName, agrega la(s) variante(s) sobre la clase base", () => {
    renderTable(
      <ZHDataTable
        columns={COLUMNS}
        rows={ROWS}
        rowKey={(r) => r.id}
        tableClassName="table--compact table--neutral"
      />,
    );
    expect(screen.getByRole("table").className).toBe("table table--compact table--neutral");
  });
});

describe("ZHDataTable — column.cellClassName (ZH-LISTING-GLOBAL-STANDARD-06)", () => {
  it("aplica la clase de celda de la columna a th y td", () => {
    const columns: ZHDataTableColumn<Row>[] = [
      { key: "code", header: "Código", render: (row) => row.code },
      { key: "name", header: "Nombre", align: "right", cellClassName: "zh-table-cell--num", render: (row) => row.name },
    ];
    renderTable(<ZHDataTable columns={columns} rows={ROWS} rowKey={(r) => r.id} />);

    const headerCell = screen.getAllByRole("columnheader")[1];
    expect(headerCell.className).toBe("zh-text-align-right zh-table-cell--num");

    const firstDataCell = screen.getAllByRole("row")[1].querySelectorAll("td")[1];
    expect(firstDataCell.className).toBe("zh-text-align-right zh-table-cell--num");
  });
});
