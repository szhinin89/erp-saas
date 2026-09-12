// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";

const listMock = vi.hoisted(() => vi.fn());
vi.mock("../api/purchaseCreditNoteService", async () => {
  const actual = await vi.importActual<typeof import("../api/purchaseCreditNoteService")>(
    "../api/purchaseCreditNoteService",
  );
  return {
    ...actual,
    purchaseCreditNoteService: { ...actual.purchaseCreditNoteService, list: listMock },
  };
});

import { PurchaseCreditNoteListPage } from "./PurchaseCreditNoteListPage";

function renderPage() {
  render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/purchases/credit-notes"]}>
        <Routes>
          <Route path="/purchases/credit-notes" element={<PurchaseCreditNoteListPage />} />
          <Route path="/purchases/credit-notes/new" element={<div>Formulario nuevo</div>} />
          <Route path="/purchases/credit-notes/:id" element={<div>Detalle NC</div>} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

// PURCHASE-CREDIT-NOTE-ENTRY-SCREEN-DUAL-MODE-01 — punto de entrada del menú: el listado, con
// un botón "Nueva" que abre la MISMA pantalla de ingreso en modo manual (sin parámetros).
describe("PurchaseCreditNoteListPage", () => {
  it("carga y muestra el listado de notas de crédito de compra", async () => {
    listMock.mockResolvedValue({
      items: [
        { id: "cn-1", purchaseInvoiceId: "inv-1", supplierId: "sup-1", applicationType: "Return",
          status: "Authorized", creditNoteNumber: "005-001-000000010", totalAmount: 115,
          issueDate: "2026-09-01", authorizedAtUtc: "2026-09-02T00:00:00Z", createdAt: "2026-09-01T00:00:00Z" },
      ],
      total: 1, page: 1, pageSize: 25,
    });

    renderPage();

    expect(await screen.findByText("005-001-000000010")).toBeTruthy();
    expect(screen.getByText("Devolución")).toBeTruthy();
    expect(listMock).toHaveBeenCalledWith({ status: undefined, page: 1, pageSize: 25 });
  });

  it('el botón "Nueva" navega a /purchases/credit-notes/new (modo manual)', async () => {
    listMock.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 25 });

    renderPage();
    await waitFor(() => expect(listMock).toHaveBeenCalledOnce());

    fireEvent.click(screen.getByRole("button", { name: "Nueva" }));

    expect(await screen.findByText("Formulario nuevo")).toBeTruthy();
  });

  it('el botón "Ver"/"Editar" de una fila navega al detalle', async () => {
    listMock.mockResolvedValue({
      items: [
        { id: "cn-1", purchaseInvoiceId: "inv-1", supplierId: "sup-1", applicationType: "Discount",
          status: "Draft", creditNoteNumber: "001-001-000000005", totalAmount: 50,
          issueDate: "2026-09-01", authorizedAtUtc: null, createdAt: "2026-09-01T00:00:00Z" },
      ],
      total: 1, page: 1, pageSize: 25,
    });

    renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "Editar" }));

    expect(await screen.findByText("Detalle NC")).toBeTruthy();
  });
});
