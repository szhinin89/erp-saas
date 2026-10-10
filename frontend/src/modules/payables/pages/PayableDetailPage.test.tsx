// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { PayableDetailPage } from "./PayableDetailPage";
import { payablesService, type PayableDetailDto } from "../api/payablesService";

/**
 * ZH-SUPPLIER-BALANCES-CROSS-LINKS-02D-F — la CxP informa el saldo a favor del proveedor (dato del
 * backend, sin consultar Saldos a favor desde aquí) y ofrece la entrada al flujo oficial: "Ver
 * saldos" (listado filtrado) y "Aplicar saldo" (detalle del saldo con la CxP preseleccionada).
 */

vi.mock("../api/payablesService", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../api/payablesService")>()),
  payablesService: { getById: vi.fn(), list: vi.fn() },
}));
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ has: () => true, canShow: () => true, isAdminRole: false }),
}));

function payable(over: Partial<PayableDetailDto> = {}): PayableDetailDto {
  return {
    id: "pay-1",
    supplierId: "sup-1",
    supplierName: "Distribuidora Andina",
    originType: "ExpenseDocument",
    originId: "exp-1",
    documentType: "EXP",
    documentNumber: "GAS-000777",
    issueDate: "2026-09-01",
    accountingDate: "2026-09-01",
    totalAmount: 80,
    paidAmount: 0,
    retainedAmount: 0,
    returnCreditAmount: 0,
    supplierCreditAmount: 0,
    creditNoteAmount: 0,
    outstandingAmount: 80,
    status: "pending",
    installments: [],
    createdAt: "2026-09-01T10:00:00Z",
    updatedAt: null,
    supplierAvailableCredit: null,
    ...over,
  };
}

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location">{location.pathname + location.search}</div>;
}

function renderPage() {
  render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/payables/pay-1"]}>
        <Routes>
          <Route path="/payables/:id" element={<PayableDetailPage />} />
          <Route path="/suppliers/credits" element={<LocationProbe />} />
          <Route path="/suppliers/credits/:id" element={<LocationProbe />} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

afterEach(() => cleanup());
beforeEach(() => vi.mocked(payablesService.getById).mockReset());

describe("PayableDetailPage — saldo a favor del proveedor (02D-F)", () => {
  it("con saldo muestra el aviso compacto con el monto a favor", async () => {
    vi.mocked(payablesService.getById).mockResolvedValue(
      payable({ supplierAvailableCredit: { availableAmount: 45.5, openCount: 2, singleOpenCreditId: null } }),
    );
    renderPage();
    await waitFor(() => expect(screen.getByText(/Este proveedor tiene \$45\.50 a favor/)).toBeTruthy());
    expect(screen.getByText("Ver saldos")).toBeTruthy();
    expect(screen.getByText("Aplicar saldo")).toBeTruthy();
  });

  it("sin saldo no muestra el aviso", async () => {
    vi.mocked(payablesService.getById).mockResolvedValue(payable());
    renderPage();
    await waitFor(() => expect(screen.getByText("Datos generales")).toBeTruthy());
    expect(screen.queryByText(/a favor/)).toBeNull();
    expect(screen.queryByText("Ver saldos")).toBeNull();
  });

  it('"Ver saldos" abre el listado de Saldos a favor filtrado por el proveedor', async () => {
    vi.mocked(payablesService.getById).mockResolvedValue(
      payable({ supplierAvailableCredit: { availableAmount: 10, openCount: 2, singleOpenCreditId: null } }),
    );
    renderPage();
    await waitFor(() => expect(screen.getByText("Ver saldos")).toBeTruthy());
    fireEvent.click(screen.getByText("Ver saldos"));
    await waitFor(() =>
      expect(screen.getByTestId("location").textContent).toBe("/suppliers/credits?supplierId=sup-1"),
    );
  });

  it('"Aplicar saldo" con un único saldo abre su detalle con esta CxP preseleccionada', async () => {
    vi.mocked(payablesService.getById).mockResolvedValue(
      payable({ supplierAvailableCredit: { availableAmount: 10, openCount: 1, singleOpenCreditId: "cred-1" } }),
    );
    renderPage();
    await waitFor(() => expect(screen.getByText("Aplicar saldo")).toBeTruthy());
    fireEvent.click(screen.getByText("Aplicar saldo"));
    await waitFor(() =>
      expect(screen.getByTestId("location").textContent).toBe("/suppliers/credits/cred-1?applyTo=pay-1"),
    );
  });

  it('"Aplicar saldo" con varios saldos abre el listado filtrado llevando la CxP destino', async () => {
    vi.mocked(payablesService.getById).mockResolvedValue(
      payable({ supplierAvailableCredit: { availableAmount: 10, openCount: 3, singleOpenCreditId: null } }),
    );
    renderPage();
    await waitFor(() => expect(screen.getByText("Aplicar saldo")).toBeTruthy());
    fireEvent.click(screen.getByText("Aplicar saldo"));
    await waitFor(() =>
      expect(screen.getByTestId("location").textContent).toBe(
        "/suppliers/credits?supplierId=sup-1&applyTo=pay-1",
      ),
    );
  });

  it("una CxP sin saldo pendiente solo ofrece ver los saldos", async () => {
    vi.mocked(payablesService.getById).mockResolvedValue(
      payable({
        outstandingAmount: 0,
        status: "paid",
        supplierAvailableCredit: { availableAmount: 10, openCount: 1, singleOpenCreditId: "cred-1" },
      }),
    );
    renderPage();
    await waitFor(() => expect(screen.getByText("Ver saldos")).toBeTruthy());
    expect(screen.queryByText("Aplicar saldo")).toBeNull();
  });
});

describe("PayableDetailPage — saldo inicial de CxP (IL-6B)", () => {
  it("muestra el origen Saldo inicial con su documento real y sin link a compra/gasto", async () => {
    vi.mocked(payablesService.getById).mockResolvedValue(
      payable({
        originType: "InitialBalance",
        originId: "row-1",
        documentType: "01",
        documentNumber: "001-001-000001234",
        issueDate: "2026-07-15",
        accountingDate: "2026-08-31",
      }),
    );
    renderPage();
    await waitFor(() => expect(screen.getAllByText("Saldo inicial").length).toBeGreaterThan(0));
    expect(screen.getByText(/001-001-000001234/)).toBeTruthy();
    expect(screen.queryByText("Ver documento")).toBeNull();
  });
});
