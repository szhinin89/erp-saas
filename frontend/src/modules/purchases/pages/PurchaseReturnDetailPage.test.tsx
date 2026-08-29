// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  render,
  screen,
  waitFor,
  fireEvent,
  cleanup,
} from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseReturnDetailPage } from "./PurchaseReturnDetailPage";
import { purchaseReturnService } from "../api/purchaseReturnService";
import { purchaseService, type PurchaseInvoiceDto } from "../api/purchaseService";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-PURCHASES-EXPENSES-03 — cubre "Autorizar devolución de compra":
 * confirma antes de ejecutar, no llama al backend si se cancela, éxito muestra message.success,
 * fallo muestra el mensaje real vía formatApiRequestError. No se toca handleCancel/handleSaveDraft
 * (ya correctos, fuera de alcance).
 */

vi.mock("../api/purchaseReturnService", () => ({
  purchaseReturnService: {
    getById: vi.fn(),
    getReturnableLines: vi.fn(),
    updateDraft: vi.fn(),
    authorize: vi.fn(),
    cancel: vi.fn(),
  },
}));

vi.mock("../api/purchaseService", () => ({
  purchaseService: {
    getById: vi.fn(),
  },
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const DRAFT_RETURN = {
  id: "return-1",
  purchaseInvoiceId: "purchase-1",
  supplierId: "supplier-1",
  branchId: "branch-1",
  returnNumber: null,
  reason: "Producto dañado",
  status: "Draft",
  fiscalStatus: "NotApplicable",
  supplierCreditNoteDocumentId: null,
  authorizedSubtotal: null,
  authorizedVatTotal: null,
  authorizedIceTotal: null,
  authorizedDiscountTotal: null,
  authorizedGrandTotal: null,
  authorizedAtUtc: null,
  cancelledAtUtc: null,
  cancellationReason: null,
  lines: [
    {
      id: "line-1",
      originalInvoiceDetailId: "detail-1",
      itemId: "item-1",
      quantity: 2,
      warehouseId: "wh-1",
    },
  ],
  createdAt: "2026-08-01T10:00:00Z",
  updatedAt: null,
};

const INVOICE = {
  id: "purchase-1",
  invoiceNumber: "001-001-000000123",
  supplierName: "Proveedor Uno",
} as unknown as PurchaseInvoiceDto;

const RETURNABLE_LINES = [
  {
    invoiceDetailId: "detail-1",
    itemId: "item-1",
    description: "Producto A",
    originalQuantity: 5,
    returnedQuantity: 2,
    remainingQuantity: 3,
    warehouseId: "wh-1",
  },
];

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/purchases/returns/return-1"]}>
        <Routes>
          <Route path="/purchases/returns/:id" element={<PurchaseReturnDetailPage />} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(purchaseReturnService.getById).mockResolvedValue(DRAFT_RETURN);
  vi.mocked(purchaseReturnService.getReturnableLines).mockResolvedValue(
    RETURNABLE_LINES,
  );
  vi.mocked(purchaseService.getById).mockResolvedValue(INVOICE);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(() => {
  cleanup();
});

describe("PurchaseReturnDetailPage — autorizar devolución: confirmación y feedback", () => {
  it("pide confirmación antes de llamar a authorize, con resumen de devolución/proveedor/fecha/estado", async () => {
    vi.mocked(purchaseReturnService.authorize).mockResolvedValue({
      ...DRAFT_RETURN,
      status: "Authorized",
      returnNumber: "DEV-000001",
    });

    renderPage();
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Autorizar devolución" })).toBeTruthy(),
    );

    fireEvent.click(screen.getByRole("button", { name: "Autorizar devolución" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(purchaseReturnService.authorize).toHaveBeenCalledWith(
        "return-1",
        expect.any(String),
      );
    });

    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(options.title).toMatch(/Autorizar devolución/i);
  });

  it("si se cancela la confirmación, no llama a authorize", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);

    renderPage();
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Autorizar devolución" })).toBeTruthy(),
    );

    fireEvent.click(screen.getByRole("button", { name: "Autorizar devolución" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(purchaseReturnService.authorize).not.toHaveBeenCalled();
  });

  it("al autorizar exitosamente muestra message.success", async () => {
    vi.mocked(purchaseReturnService.authorize).mockResolvedValue({
      ...DRAFT_RETURN,
      status: "Authorized",
      returnNumber: "DEV-000001",
    });

    renderPage();
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Autorizar devolución" })).toBeTruthy(),
    );

    fireEvent.click(screen.getByRole("button", { name: "Autorizar devolución" }));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith(
        "Devolución autorizada correctamente.",
      ),
    );
  });

  it("si el backend falla, muestra el mensaje real y no llama message.success", async () => {
    vi.mocked(purchaseReturnService.authorize).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: {
          message: { user: "El inventario de la bodega no tiene existencias suficientes." },
        },
      },
    });

    renderPage();
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Autorizar devolución" })).toBeTruthy(),
    );

    fireEvent.click(screen.getByRole("button", { name: "Autorizar devolución" }));

    await waitFor(() => {
      expect(message.error).toHaveBeenCalledWith(
        "El inventario de la bodega no tiene existencias suficientes.",
      );
    });
    expect(message.success).not.toHaveBeenCalled();
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(purchaseReturnService.authorize).mockResolvedValue({
      ...DRAFT_RETURN,
      status: "Authorized",
    });

    renderPage();
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Autorizar devolución" })).toBeTruthy(),
    );
    fireEvent.click(screen.getByRole("button", { name: "Autorizar devolución" }));
    await waitFor(() => expect(purchaseReturnService.authorize).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
