// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { ImportWizardPage } from "./ImportWizardPage";
import { useImportWizard } from "./useImportWizard";

vi.mock("./useImportWizard", () => ({ useImportWizard: vi.fn() }));
vi.mock("../../../components/items/ItemCodeConflicts", () => ({ ItemCodeConflicts: () => null }));

const noop = vi.fn();

function issue(rowNumber: number, code: string, message: string) {
  return { id: `${code}-${rowNumber}`, rowNumber, fieldName: null, severity: "Warning", code, message };
}

function row(rowNumber: number) {
  return {
    id: `r${rowNumber}`,
    rowNumber,
    rawData: {},
    isImported: false,
    hasBlockingIssue: false,
    issues: [
      issue(rowNumber, "CATEGORY_WILL_BE_CREATED", "La categoría 'Abarrotes' no existe — se creará automáticamente al confirmar."),
      issue(rowNumber, "BRAND_WILL_BE_CREATED", "La marca 'Marca X' no existe — se creará automáticamente al confirmar."),
    ],
  };
}

afterEach(cleanup);

describe("ImportWizardPage — aviso de creación de catálogo (IL-1)", () => {
  it("cuenta categorías y marcas distintas, no filas que las repiten", () => {
    vi.mocked(useImportWizard).mockReturnValue({
      batch: {
        id: "b1",
        importType: "Items",
        status: "Validated",
        totalRows: 3,
        validRows: 3,
        issueRows: 0,
        warningRows: 3,
        autoCreateCatalogValues: true,
      },
      step: "validated",
      uploadProgress: 0,
      error: null,
      preview: { items: [row(1), row(2), row(3)], totalCount: 3 },
      previewPage: 1,
      previewLoading: false,
      severityFilter: "all",
      confirmModalOpen: true,
      confirmResult: null,
      autoCreateCatalogValues: true,
      setAutoCreateCatalogValues: noop,
      downloadTemplate: noop,
      handleFileSelected: noop,
      loadPreview: noop,
      changeSeverityFilter: noop,
      setConfirmModalOpen: noop,
      confirmBatch: noop,
      reset: noop,
    } as unknown as ReturnType<typeof useImportWizard>);

    render(
      <MemoryRouter>
        <ImportWizardPage
          importType="Items"
          templateFileName="plantilla.xlsx"
          title="Carga Inicial — Catálogo de Productos"
          helpText=""
          requiredFieldsHint=""
          resultRoute="/products/items"
          resultRouteLabel="Ver ítems"
          resultEntityLabelPlural="productos"
          showAutoCreateCatalogOption
        />
      </MemoryRouter>,
    );

    expect(screen.getByText(/El preview muestra 1 categoría y 1 marca pendientes de creación\./)).toBeTruthy();
  });
});
