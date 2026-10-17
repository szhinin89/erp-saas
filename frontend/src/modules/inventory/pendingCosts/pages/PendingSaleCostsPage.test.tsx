// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../../i18n/i18n";
import { PendingSaleCostsPage } from "./PendingSaleCostsPage";

/** ACCOUNTING-12B — pendientes de costo con contexto legible, sin "Contabilizar", y estado vacío. */

const mocks = vi.hoisted(() => ({ getPendingCosts: vi.fn() }));
vi.mock("../../stock/api/stockService", () => ({
  stockService: { getPendingCosts: mocks.getPendingCosts },
}));
vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: () => true, has: () => true, isAdminRole: false }),
}));

const renderPage = () =>
  render(
    <I18nProvider>
      <MemoryRouter>
        <PendingSaleCostsPage />
      </MemoryRouter>
    </I18nProvider>,
  );

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

it("A12B: renderiza pendientes con contexto y enlaces de regularización, o estado vacío", async () => {
  mocks.getPendingCosts.mockResolvedValue([
    {
      obligationId: "o-1",
      invoiceId: "i-1",
      invoiceNumber: "001-001-000000123",
      issueDate: "2026-10-15",
      invoiceLineId: "l-1",
      productId: "p-1",
      itemSku: "SKU-1",
      itemName: "Arroz 1kg",
      warehouseId: "w-1",
      warehouseName: "Bodega Principal",
      pendingQuantity: 3,
      provisionalUnitCost: null,
      status: "CostPending",
    },
  ]);
  renderPage();
  expect(await screen.findByText("001-001-000000123")).toBeTruthy();
  expect(screen.getByText("SKU-1")).toBeTruthy();
  expect(screen.getByText("Bodega Principal")).toBeTruthy();
  expect(screen.getByText("Sin costo")).toBeTruthy();
  expect(screen.getByText(/Se regularizará automáticamente/)).toBeTruthy();
  expect(screen.getByRole("link", { name: "Registrar compra" }).getAttribute("href")).toBe("/purchases");
  expect(screen.getByRole("link", { name: "Ajuste positivo con costo" }).getAttribute("href")).toBe(
    "/inventory/adjustments/new",
  );
  expect(screen.queryByRole("button", { name: /Contabilizar/ })).toBeNull();
  cleanup();

  mocks.getPendingCosts.mockResolvedValue([]);
  renderPage();
  expect(await screen.findByText(/No hay ventas con costo pendiente/)).toBeTruthy();
});
