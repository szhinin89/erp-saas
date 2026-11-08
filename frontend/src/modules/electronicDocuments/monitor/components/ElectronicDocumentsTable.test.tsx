// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";
import { ElectronicDocumentsTable } from "./ElectronicDocumentsTable";
import type { ElectronicDocumentListItemDto } from "../api/electronicDocumentsMonitorService";

/**
 * ZH-LISTING-COMPLIANCE-AUDIT-08 — /electronic-documents/monitor es un listado principal
 * (paginado server-side): debe mostrar "N°" como primera columna sin perder ninguna de las
 * columnas funcionales (Estado, Tipo, Número, Cliente/Proveedor, Empresa, Fecha, Ambiente,
 * Clave de acceso, Intentos, Última actualización, Último mensaje) ni las acciones existentes.
 */

const ITEM: ElectronicDocumentListItemDto = {
  id: "doc-1",
  createdAt: "2026-08-01T10:00:00Z",
  documentType: "Invoice",
  documentNumber: "001-001-000000123",
  companyId: "company-1",
  companyName: "ZH Technologies",
  counterpartyName: "Juan Pérez",
  currentState: "Authorized",
  environment: "2",
  accessKey: "1234567890abcdef",
  retryCount: 0,
  updatedAt: "2026-08-01T10:05:00Z",
  lastMessage: "Autorizado por el SRI",
};

function renderTable(props: Partial<Parameters<typeof ElectronicDocumentsTable>[0]> = {}) {
  return render(
    <I18nProvider>
      <ElectronicDocumentsTable
        items={[ITEM]}
        loading={false}
        total={1}
        page={1}
        pageSize={25}
        onPageChange={vi.fn()}
        onSelect={vi.fn()}
        {...props}
      />
    </I18nProvider>,
  );
}

afterEach(() => cleanup());

describe("ElectronicDocumentsTable — ZH-LISTING-COMPLIANCE-AUDIT-08", () => {
  it('muestra "N°" como primera columna', () => {
    renderTable();
    const headers = screen.getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers[0]).toBe("N°");
  });

  it("la primera fila muestra 1 en la columna N°", () => {
    renderTable();
    const rows = screen.getAllByRole("row").slice(1);
    const firstCell = within(rows[0]).getAllByRole("cell")[0];
    expect(firstCell.textContent).toBe("1");
  });

  it("con paginación server-side, la numeración respeta el offset de página", () => {
    renderTable({ page: 3, pageSize: 25, total: 100 });
    const rows = screen.getAllByRole("row").slice(1);
    const firstCell = within(rows[0]).getAllByRole("cell")[0];
    // (page 3 - 1) * pageSize 25 + 1 = 51
    expect(firstCell.textContent).toBe("51");
  });

  it("conserva las columnas funcionales: número de documento, empresa y contraparte", () => {
    renderTable();
    expect(screen.getByText("001-001-000000123")).toBeTruthy();
    expect(screen.getByText("ZH Technologies")).toBeTruthy();
    expect(screen.getByText("Juan Pérez")).toBeTruthy();
  });

  it("no pierde el badge de estado ni el botón de copiar clave de acceso", () => {
    renderTable();
    expect(document.querySelector(".zh-edoc-state-badge-label")).toBeTruthy();
    expect(screen.getByRole("button", { name: /clave de acceso|copy/i })).toBeTruthy();
  });

  it("el click en una fila sigue invocando onSelect con el id del documento", () => {
    const onSelect = vi.fn();
    renderTable({ onSelect });
    const rows = screen.getAllByRole("row").slice(1);
    rows[0].click();
    expect(onSelect).toHaveBeenCalledWith("doc-1");
  });
});


// SALES-ELECTRONIC-STATUS-CLARITY-01 — explicación breve de los estados que se confunden con
// un comprobante ya autorizado (Borrador = sin emitir; Firmado ≠ autorizado).
describe("ElectronicDocumentsTable — explicación de estados", () => {
  it("Borrador explica que aún no se emite (sin XML firmado ni autorización SRI)", () => {
    renderTable({ items: [{ ...ITEM, currentState: "Draft" }] });
    const badge = screen.getByText("Borrador").closest("[title]");
    expect(badge?.getAttribute("title")).toBe(
      "Sin emitir: aún no tiene XML firmado ni autorización del SRI.",
    );
  });

  it("Firmado aclara que todavía no está autorizado por el SRI", () => {
    renderTable({ items: [{ ...ITEM, currentState: "Signed" }] });
    const badge = screen.getByText("Firmado").closest("[title]");
    expect(badge?.getAttribute("title")).toBe("XML firmado, pero aún no autorizado por el SRI.");
  });

  it("Rechazado no lleva explicación (no se presta a confusión)", () => {
    renderTable({ items: [{ ...ITEM, currentState: "Rejected" }] });
    expect(screen.getByText("Rechazado").closest("[title]")).toBeNull();
  });
});

// SRI-MONITOR-UX-01 — intentos solo en su columna, Reintentos agotados, acceso por teclado y
// variante compacta "Requieren atención".
describe("ElectronicDocumentsTable — SRI-MONITOR-UX-01", () => {
  const SIGNED_MANY: ElectronicDocumentListItemDto = {
    ...ITEM,
    id: "doc-2",
    currentState: "Signed",
    retryCount: 37,
    lastMessage: "CLAVE ACCESO EN PROCESAMIENTO",
  };

  /** Celda por encabezado de columna (fila 1). */
  const cellOf = (header: string) => {
    const headers = screen.getAllByRole("columnheader").map((th) => th.textContent);
    const row = screen.getAllByRole("row")[1];
    return within(row).getAllByRole("cell")[headers.indexOf(header)];
  };

  it("el badge no repite el número de intentos; Fechas e intentos lo agrupa", () => {
    renderTable({ items: [SIGNED_MANY] });
    const badge = document.querySelector(".zh-edoc-state-badge-label");
    expect(badge?.textContent).toContain("Firmado");
    expect(badge?.textContent).not.toMatch(/37|intento/i);
    expect(cellOf("Fechas e intentos").textContent).toContain("Intentos: 37");
    expect(screen.getByText("CLAVE ACCESO EN PROCESAMIENTO")).toBeTruthy();
  });

  it("DeadLetter se muestra como «Reintentos agotados» (único origen: MarkDeadLetter por reintentos agotados)", () => {
    renderTable({ items: [{ ...ITEM, currentState: "DeadLetter", retryCount: 5 }] });
    expect(screen.getByText("Reintentos agotados")).toBeTruthy();
    expect(screen.queryByText("Con error")).toBeNull();
  });

  it("cada fila ofrece un botón nativo «Ver detalle» con nombre accesible propio", () => {
    const onSelect = vi.fn();
    renderTable({ onSelect });
    const button = screen.getByRole("button", {
      name: "Ver detalle de 001-001-000000123",
    });
    expect(button.tagName).toBe("BUTTON");
    button.focus();
    expect(document.activeElement).toBe(button);
    fireEvent.click(button);
    // stopPropagation: el clic del botón no se suma al clic de la fila.
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect).toHaveBeenCalledWith("doc-1");
  });

  it("sin número de documento, el nombre accesible usa el tipo de documento", () => {
    renderTable({ items: [{ ...ITEM, documentNumber: null }] });
    expect(screen.getByRole("button", { name: "Ver detalle de Factura" })).toBeTruthy();
  });

  it("ZH-DS-TABLE-DENSITY-01: columnas agrupadas con línea principal y secundaria", () => {
    renderTable();
    const headers = screen.getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers).toEqual([
      "N°",
      "Acciones",
      "Documento",
      "Estado",
      "Cliente / Proveedor · Empresa",
      "Fechas e intentos",
      "SRI / Acceso",
    ]);
    const lines = (header: string) =>
      Array.from(cellOf(header).children).map((el) => el.textContent);
    expect(lines("Documento")).toEqual(["001-001-000000123", "Factura"]);
    expect(lines("Estado")[1]).toBe("Producción");
    // El listado puede abarcar varias empresas del tenant: la empresa se conserva como secundaria.
    expect(lines("Cliente / Proveedor · Empresa")).toEqual(["Juan Pérez", "ZH Technologies"]);
    expect(lines("Fechas e intentos")[1]).toMatch(/^Actualizado .+ · Intentos: 0$/);
    expect(lines("SRI / Acceso")[0]).toBe("Autorizado por el SRI");
    expect(lines("SRI / Acceso")[1]).toContain("1234567890…");
    for (const header of ["Documento", "Estado", "Cliente / Proveedor · Empresa", "Fechas e intentos", "SRI / Acceso"])
      expect(cellOf(header).children.length).toBeLessThanOrEqual(2);
  });

  it("sin mensaje ni clave lo indica en lugar de dejar la celda vacía", () => {
    renderTable({ items: [{ ...ITEM, lastMessage: null, accessKey: null }] });
    expect(cellOf("SRI / Acceso").textContent).toContain("Sin mensajes del SRI");
    expect(cellOf("SRI / Acceso").textContent).toContain("Aún no generada");
  });

  it("usa las variantes oficiales compacta y alineada arriba de ZHDataTable", () => {
    renderTable();
    const table = document.querySelector("table");
    expect(table?.className).toContain("table--compact");
    expect(table?.className).toContain("table--align-top");
  });

  it("variante attention: columnas de seguimiento, sin N° ni paginación", () => {
    render(
      <I18nProvider>
        <ElectronicDocumentsTable
          variant="attention"
          items={[SIGNED_MANY]}
          loading={false}
          onSelect={vi.fn()}
          emptyMessage="vacío"
        />
      </I18nProvider>,
    );
    const headers = screen.getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers).toEqual([
      "Acciones",
      "Documento",
      "Estado",
      "Fechas e intentos",
      "SRI / Acceso",
    ]);
    expect(screen.queryByRole("navigation")).toBeNull();
    expect(screen.queryByText(/página/i)).toBeNull();
  });
});
