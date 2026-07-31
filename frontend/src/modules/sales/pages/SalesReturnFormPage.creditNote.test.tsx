// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, cleanup, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import type { SalesReturnDto } from "../api/salesReturnService";

// Mismo criterio que SalesPage.ride.test.tsx: esta suite prueba únicamente la visibilidad
// condicional de la sección de Nota de Crédito Electrónica agregada en la Fase 14, no el resto
// del formulario de devoluciones ya existente (Fases 4-13, sin cambios).
vi.mock("../components/SalesReturnInvoicePicker", () => ({
  SalesReturnInvoicePicker: () => null,
}));
vi.mock("../components/ReturnableLinesEditor", () => ({
  ReturnableLinesEditor: () => null,
}));
vi.mock("../components/AuthorizeSalesReturnModal", () => ({
  AuthorizeSalesReturnModal: () => null,
}));
vi.mock("../components/SalesReturnSummary", () => ({
  SalesReturnSummary: () => null,
}));

const creditNoteSectionSpy = vi.fn();
vi.mock("../components/SalesReturnCreditNoteSection", () => ({
  SalesReturnCreditNoteSection: (props: {
    salesReturnId: string;
    returnNumber: string;
  }) => {
    creditNoteSectionSpy(props);
    return <div data-testid="credit-note-section" />;
  },
}));

const getByIdMock = vi.fn();
vi.mock("../api/salesReturnService", async () => {
  const actual = await vi.importActual<
    typeof import("../api/salesReturnService")
  >("../api/salesReturnService");
  return {
    ...actual,
    salesReturnService: {
      ...actual.salesReturnService,
      getById: (...a: unknown[]) => getByIdMock(...a),
    },
  };
});

vi.mock("../api/salesService", () => ({
  salesService: {
    getById: vi.fn().mockRejectedValue(new Error("not needed in this test")),
  },
}));

import { SalesReturnFormPage } from "./SalesReturnFormPage";

function buildReturn(overrides: Partial<SalesReturnDto> = {}): SalesReturnDto {
  return {
    id: "return-1",
    salesInvoiceId: "invoice-1",
    customerId: "customer-1",
    returnNumber: "DEV-001-001-000000001",
    reason: "Producto en mal estado",
    status: "Draft",
    subtotal: 20,
    totalVat: 3,
    totalIce: 0,
    totalDiscount: 0,
    grandTotal: 23,
    lines: [],
    refundAllocations: [],
    createdAt: "2026-07-30T00:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/sales/returns/return-1"]}>
      <Routes>
        <Route path="/sales/returns/:id" element={<SalesReturnFormPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("SalesReturnFormPage — sección Nota de Crédito Electrónica (Fase 14)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it("no muestra la sección de Nota de Crédito mientras la devolución está en Draft", async () => {
    getByIdMock.mockResolvedValue(buildReturn({ status: "Draft" }));

    renderPage();

    await waitFor(() => expect(getByIdMock).toHaveBeenCalled());
    expect(screen.queryByTestId("credit-note-section")).toBeNull();
  });

  it("muestra la sección de Nota de Crédito cuando la devolución está Authorized, con el id y número reales", async () => {
    getByIdMock.mockResolvedValue(
      buildReturn({
        id: "return-42",
        returnNumber: "DEV-001-001-000000042",
        status: "Authorized",
      }),
    );

    renderPage();

    await waitFor(() =>
      expect(screen.queryByTestId("credit-note-section")).not.toBeNull(),
    );
    expect(creditNoteSectionSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        salesReturnId: "return-42",
        returnNumber: "DEV-001-001-000000042",
      }),
    );
  });

  it("no muestra la sección de Nota de Crédito para una devolución Cancelled", async () => {
    getByIdMock.mockResolvedValue(buildReturn({ status: "Cancelled" }));

    renderPage();

    await waitFor(() => expect(getByIdMock).toHaveBeenCalled());
    expect(screen.queryByTestId("credit-note-section")).toBeNull();
  });
});
