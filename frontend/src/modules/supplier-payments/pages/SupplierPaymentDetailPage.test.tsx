// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { SupplierPaymentDetailPage } from "./SupplierPaymentDetailPage";
import { supplierPaymentService, type SupplierPaymentDto } from "../api/supplierPaymentService";
import { paymentMethodLookupFacade } from "../../sales/facades/paymentMethodLookupFacade";
import { financialDestinationService } from "../../finance/api/financialDestinationService";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";

const routeParams: { id?: string } = { id: "sp-1" };

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return { ...actual, useNavigate: () => vi.fn(), useParams: () => routeParams };
});

vi.mock("../api/supplierPaymentService", () => ({
  supplierPaymentService: { getById: vi.fn(), reverse: vi.fn(), list: vi.fn(), register: vi.fn() },
}));

vi.mock("../../sales/facades/paymentMethodLookupFacade", () => ({
  paymentMethodLookupFacade: { list: vi.fn() },
}));

vi.mock("../../finance/api/financialDestinationService", () => ({
  financialDestinationService: { list: vi.fn() },
}));

vi.mock("../../masterData/api/businessPartnerFacade", () => ({
  businessPartnerFacade: { getBusinessPartner: vi.fn() },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}));

function grant(granted: string[]) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (key: string) => granted.includes(key),
    has: (key: string) => granted.includes(key),
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
}

function payment(over: Partial<SupplierPaymentDto> = {}): SupplierPaymentDto {
  return {
    id: "sp-1",
    supplierId: "sup-1",
    branchId: "br-1",
    paymentDate: "2026-08-28",
    totalAmount: 300,
    systemNumber: "00000001",
    receiptNumber: null,
    displayNumber: "00000001",
    status: "Confirmed",
    methodLines: [
      {
        id: "ml-1",
        paymentMethodId: "pm-1",
        financialDestinationId: "fd-1",
        amount: 300,
        referenceNumber: null,
        checkNumber: null,
        checkDate: null,
        notes: null,
      },
    ],
    applicationLines: [{ id: "al-1", accountsPayableInstallmentId: "inst-1", amountApplied: 300 }],
    allocations: [],
    createdAt: "2026-08-28T10:00:00Z",
    ...over,
  };
}

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <SupplierPaymentDetailPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

beforeEach(() => {
  routeParams.id = "sp-1";
  grant(["supplier-payments.view", "supplier-payments.reverse"]);
  vi.mocked(paymentMethodLookupFacade.list).mockResolvedValue([]);
  vi.mocked(financialDestinationService.list).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue({
    legalName: "Proveedor Test",
    tradeName: null,
  } as Awaited<ReturnType<typeof businessPartnerFacade.getBusinessPartner>>);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("SupplierPaymentDetailPage — acción de reversa", () => {
  it('muestra "Reversar pago" cuando status es Confirmed y hay permiso', async () => {
    vi.mocked(supplierPaymentService.getById).mockResolvedValue(payment({ status: "Confirmed" }));

    renderPage();

    expect(await screen.findByText("Reversar pago")).toBeTruthy();
  });

  it("no muestra el botón cuando status es Reversed", async () => {
    vi.mocked(supplierPaymentService.getById).mockResolvedValue(
      payment({
        status: "Reversed",
        reversedAtUtc: "2026-08-29T10:00:00Z",
        reversedBy: "user-1",
        reverseReason: "Duplicado",
      }),
    );

    renderPage();

    await screen.findByText("Proveedor Test");
    expect(screen.queryByText("Reversar pago")).toBeNull();
    // El detalle debe mostrar los campos de reversa cuando existen.
    expect(screen.getByText("Duplicado")).toBeTruthy();
  });

  it("no muestra el botón cuando falta el permiso supplier-payments.reverse", async () => {
    grant(["supplier-payments.view"]);
    vi.mocked(supplierPaymentService.getById).mockResolvedValue(payment({ status: "Confirmed" }));

    renderPage();

    await screen.findByText("Proveedor Test");
    expect(screen.queryByText("Reversar pago")).toBeNull();
  });

  it("al confirmar la reversa exitosamente, recarga el detalle", async () => {
    vi.mocked(supplierPaymentService.getById)
      .mockResolvedValueOnce(payment({ status: "Confirmed" }))
      .mockResolvedValueOnce(payment({ status: "Reversed", reverseReason: "Duplicado" }));
    vi.mocked(supplierPaymentService.reverse).mockResolvedValue(
      payment({ status: "Reversed", reverseReason: "Duplicado" }),
    );

    renderPage();

    fireEvent.click(await screen.findByText("Reversar pago"));
    fireEvent.change(await screen.findByLabelText("Motivo de la reversa"), {
      target: { value: "Duplicado" },
    });
    fireEvent.click(screen.getByText("Confirmar reversa"));

    await waitFor(() =>
      expect(supplierPaymentService.reverse).toHaveBeenCalledWith("sp-1", "Duplicado"),
    );
    await waitFor(() => expect(supplierPaymentService.getById).toHaveBeenCalledTimes(2));
    // El modal se cierra y el detalle refleja el nuevo estado Reversed.
    await waitFor(() => expect(screen.queryByText("Reversar pago")).toBeNull());
  });

  it("si la API falla, no cambia el estado local y muestra el error en el modal", async () => {
    vi.mocked(supplierPaymentService.getById).mockResolvedValue(payment({ status: "Confirmed" }));
    vi.mocked(supplierPaymentService.reverse).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 422,
        data: { data: { errors: ["El pago ya fue reversado."] } },
      },
    });

    renderPage();

    fireEvent.click(await screen.findByText("Reversar pago"));
    fireEvent.change(await screen.findByLabelText("Motivo de la reversa"), {
      target: { value: "Intento" },
    });
    fireEvent.click(screen.getByText("Confirmar reversa"));

    expect(await screen.findByText("El pago ya fue reversado.")).toBeTruthy();
    // getById solo se llamó una vez (carga inicial) — el fallo no dispara una recarga.
    expect(supplierPaymentService.getById).toHaveBeenCalledTimes(1);
    // El botón de acción sigue disponible (el título del modal también dice "Reversar pago",
    // por eso se filtra por el <button>) — el pago local sigue Confirmed.
    expect(
      screen.getAllByText("Reversar pago").some((el) => el.closest("button")),
    ).toBe(true);
  });
});
