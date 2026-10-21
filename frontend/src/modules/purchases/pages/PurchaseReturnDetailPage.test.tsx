// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseReturnDetailPage } from "./PurchaseReturnDetailPage";

vi.mock("../api/purchaseReturnService", () => ({ purchaseReturnService: {
  getById: vi.fn().mockResolvedValue({ id: "return-1", returnNumber: "DEV-001",
    purchaseInvoiceNumber: "001-001-000000001", status: "Authorized",
    fiscalStatus: "PendingSupplierCreditNote", reason: "Producto dañado",
    createdAt: "2026-09-01T12:00:00Z", authorizedGrandTotal: 112,
    lines: [{ id: "line-1", itemName: "Producto histórico", quantity: 1, warehouseName: "Matriz" }] }),
} }));
afterEach(cleanup);

it("conserva el historial consultable sin operaciones independientes", async () => {
  render(<I18nProvider><MemoryRouter initialEntries={["/purchases/returns/return-1"]}>
    <Routes><Route path="/purchases/returns/:id" element={<PurchaseReturnDetailPage />} /></Routes>
  </MemoryRouter></I18nProvider>);
  expect(await screen.findByText("Producto histórico")).toBeTruthy();
  expect(screen.getByText("Producto dañado")).toBeTruthy();
  expect(screen.getByText(/Solo consulta/)).toBeTruthy();
  expect(screen.queryByRole("button", { name: /autorizar|cancelar|guardar|vincular/i })).toBeNull();
});
