// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { PriceListCustomersTab } from "./PriceListCustomersTab";
import { priceListCustomerService, type PriceListDto } from "../api/pricingService";
import { customerLookupFacade } from "../../masterData/facades/customerLookupFacade";
import { message } from "../../../lib/messages";

// PRICING-CUSTOMER-PRICE-LIST-ADMIN-05B: administración de PriceListCustomer desde
// /products/pricing — reutiliza el mismo patrón de mocking que PriceListExceptionsTab.test.tsx.

vi.mock("../api/pricingService", async () => {
  const actual = await vi.importActual<typeof import("../api/pricingService")>(
    "../api/pricingService",
  );
  return {
    ...actual,
    priceListCustomerService: {
      list: vi.fn(),
      assign: vi.fn(),
      remove: vi.fn(),
    },
  };
});

vi.mock("../../masterData/facades/customerLookupFacade", () => ({
  customerLookupFacade: {
    searchCustomers: vi.fn(),
  },
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const PRICE_LIST: PriceListDto = {
  id: "pl-1",
  code: "MAYORISTA",
  name: "Mayoristas",
  currencyCode: "USD",
  isDefault: false,
  validFrom: null,
  validUntil: null,
  ruleType: "PercentDiscount",
  ruleValue: 10,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: null,
};

const ASSIGNED_CUSTOMER = {
  customerId: "cust-1",
  customerName: "Cliente Uno",
  customerIdentificationNumber: "1710034065",
};

const SEARCH_RESULT = {
  id: "cust-2",
  identificationNumber: "0912345678",
  fullName: "Cliente Nuevo",
  isActive: true,
  hasCustomerRole: true,
};

function show() {
  return render(
    <I18nProvider>
      <PriceListCustomersTab priceList={PRICE_LIST} />
    </I18nProvider>,
  );
}

afterEach(() => cleanup());

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(priceListCustomerService.list).mockResolvedValue([ASSIGNED_CUSTOMER]);
  vi.mocked(customerLookupFacade.searchCustomers).mockResolvedValue([SEARCH_RESULT]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("PriceListCustomersTab", () => {
  it("lista los clientes ya asignados", async () => {
    show();
    expect(await screen.findByText("Cliente Uno")).toBeTruthy();
    expect(screen.getByText("1710034065")).toBeTruthy();
  });

  it("muestra estado vacío cuando la lista no tiene clientes", async () => {
    vi.mocked(priceListCustomerService.list).mockResolvedValue([]);
    show();
    expect(
      await screen.findByText("Esta lista todavía no tiene clientes asignados."),
    ).toBeTruthy();
  });

  it("busca y asigna un cliente nuevo (sin conflicto)", async () => {
    vi.mocked(priceListCustomerService.assign).mockResolvedValue({
      status: "Assigned",
      conflictingPriceListId: null,
      conflictingPriceListName: null,
    });
    show();
    await screen.findByText("Cliente Uno");

    fireEvent.change(screen.getByPlaceholderText("Buscar cliente por nombre o identificación..."), {
      target: { value: "Nuevo" },
    });
    const result = await screen.findByText("Cliente Nuevo");
    fireEvent.click(result);

    await waitFor(() =>
      expect(priceListCustomerService.assign).toHaveBeenCalledWith("pl-1", "cust-2", false),
    );
    await waitFor(() => expect(message.success).toHaveBeenCalledWith("Cliente asignado a la lista."));
    expect(priceListCustomerService.list).toHaveBeenCalledTimes(2);
  });

  it("quita un cliente tras confirmar", async () => {
    vi.mocked(priceListCustomerService.remove).mockResolvedValue(true);
    show();
    await screen.findByText("Cliente Uno");

    fireEvent.click(screen.getByTitle("Quitar cliente"));

    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(priceListCustomerService.remove).toHaveBeenCalledWith("pl-1", "cust-1"),
    );
    expect(message.success).toHaveBeenCalledWith("Cliente quitado de la lista.");
  });

  it("no quita el cliente si se cancela la confirmación", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    show();
    await screen.findByText("Cliente Uno");

    fireEvent.click(screen.getByTitle("Quitar cliente"));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(priceListCustomerService.remove).not.toHaveBeenCalled();
  });

  it("muestra confirmación cuando el cliente ya tiene otra lista activa (Conflict) — no llama assign de nuevo hasta confirmar", async () => {
    vi.mocked(priceListCustomerService.assign).mockResolvedValue({
      status: "Conflict",
      conflictingPriceListId: "pl-0",
      conflictingPriceListName: "General",
    });
    show();
    await screen.findByText("Cliente Uno");

    fireEvent.change(screen.getByPlaceholderText("Buscar cliente por nombre o identificación..."), {
      target: { value: "Nuevo" },
    });
    fireEvent.click(await screen.findByText("Cliente Nuevo"));

    await waitFor(() =>
      expect(
        screen.getByText("Este cliente actualmente usa General. ¿Desea cambiarlo a Mayoristas?"),
      ).toBeTruthy(),
    );
    expect(priceListCustomerService.assign).toHaveBeenCalledTimes(1);
  });

  it("al confirmar el cambio, reenvía assign con confirmSwitch=true", async () => {
    vi.mocked(priceListCustomerService.assign)
      .mockResolvedValueOnce({
        status: "Conflict",
        conflictingPriceListId: "pl-0",
        conflictingPriceListName: "General",
      })
      .mockResolvedValueOnce({
        status: "Switched",
        conflictingPriceListId: null,
        conflictingPriceListName: null,
      });
    show();
    await screen.findByText("Cliente Uno");

    fireEvent.change(screen.getByPlaceholderText("Buscar cliente por nombre o identificación..."), {
      target: { value: "Nuevo" },
    });
    fireEvent.click(await screen.findByText("Cliente Nuevo"));
    await screen.findByText("Este cliente actualmente usa General. ¿Desea cambiarlo a Mayoristas?");

    fireEvent.click(screen.getByRole("button", { name: "Cambiar" }));

    await waitFor(() =>
      expect(priceListCustomerService.assign).toHaveBeenNthCalledWith(2, "pl-1", "cust-2", true),
    );
    await waitFor(() => expect(message.success).toHaveBeenCalledWith("Cliente movido a esta lista."));
  });

  it("muestra el mensaje de negocio del backend en vez de un error técnico", async () => {
    vi.mocked(priceListCustomerService.list).mockRejectedValue({
      isAxiosError: true,
      message: "Request failed with status code 400",
      response: { status: 400, data: { message: { user: "No se pudo cargar la lista de clientes." } } },
    });
    show();
    expect(await screen.findByText("No se pudo cargar la lista de clientes.")).toBeTruthy();
    expect(screen.queryByText(/Request failed/)).toBeNull();
  });
});
