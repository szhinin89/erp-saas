// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { InitialLoadInitialPayablesPage } from "./InitialLoadInitialPayablesPage";
import { useImportWizard } from "./useImportWizard";
import { payableOriginLabel } from "../../../lib/payableOrigin";

vi.mock("./useImportWizard", () => ({ useImportWizard: vi.fn() }));

const noop = vi.fn();

function mockValidatedBatch(issueRows: number) {
  vi.mocked(useImportWizard).mockReturnValue({
    batch: {
      id: "b1",
      importType: "InitialPayables",
      status: "Validated",
      totalRows: 2,
      validRows: 2 - issueRows,
      issueRows,
      warningRows: 0,
    },
    step: "validated",
    uploadProgress: 0,
    error: null,
    preview: { items: [], totalCount: 0 },
    previewPage: 1,
    previewLoading: false,
    severityFilter: "all",
    confirmModalOpen: false,
    confirmResult: null,
    autoCreateCatalogValues: false,
    setAutoCreateCatalogValues: noop,
    downloadTemplate: noop,
    handleFileSelected: noop,
    loadPreview: noop,
    changeSeverityFilter: noop,
    setConfirmModalOpen: noop,
    confirmBatch: noop,
    reset: noop,
  } as unknown as ReturnType<typeof useImportWizard>);
}

afterEach(cleanup);

describe("InitialLoadInitialPayablesPage (IL-6A)", () => {
  it("usa la plantilla de CxP inicial", () => {
    mockValidatedBatch(0);
    render(<MemoryRouter><InitialLoadInitialPayablesPage /></MemoryRouter>);

    expect(vi.mocked(useImportWizard)).toHaveBeenCalledWith("InitialPayables", "plantilla-cxp-inicial.xlsx");
    expect(screen.getByText("Carga Inicial — Cuentas por Pagar")).toBeTruthy();
  });

  it("permite validar y revisar pero mantiene la confirmación bloqueada aunque todo sea válido", () => {
    mockValidatedBatch(0);
    render(<MemoryRouter><InitialLoadInitialPayablesPage /></MemoryRouter>);

    expect(screen.getByText(/confirmación de CxP inicial todavía no está disponible/)).toBeTruthy();
    const confirm = screen.getByRole("button", { name: "Confirmar importación" }) as HTMLButtonElement;
    expect(confirm.disabled).toBe(true);
  });

  it("etiqueta el origen InitialBalance de la CxP", () => {
    expect(payableOriginLabel("InitialBalance")).toBe("Saldo inicial");
  });
});
