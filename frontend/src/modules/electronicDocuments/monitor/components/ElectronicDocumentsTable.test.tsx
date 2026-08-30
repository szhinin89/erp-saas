// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
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
    expect(document.querySelector(".edm-state-badge-label")).toBeTruthy();
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
