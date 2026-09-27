// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { CashFundingRequestsPendingNotice } from "./CashFundingRequestsPendingNotice";
import { cashFundingRequestService } from "../api/cashFundingRequestService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";

vi.mock("../api/cashFundingRequestService", () => ({ cashFundingRequestService: { list: vi.fn() } }));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));

function grant(...keys: string[]) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (k: string) => keys.includes(k),
    has: (k: string) => keys.includes(k),
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
}

const renderNotice = () =>
  render(
    <I18nProvider>
      <MemoryRouter>
        <CashFundingRequestsPendingNotice />
      </MemoryRouter>
    </I18nProvider>,
  );

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("CashFundingRequestsPendingNotice", () => {
  it("15. el contador de Caja es el totalCount de la bandeja filtrada por Pending", async () => {
    grant("caja.funding-requests.view");
    vi.mocked(cashFundingRequestService.list).mockResolvedValue({ items: [], pageNumber: 1, pageSize: 1, totalCount: 3 });
    renderNotice();

    expect(await screen.findByText("Solicitudes de efectivo: 3")).toBeTruthy();
    expect(screen.getByText("Ver")).toBeTruthy();
    expect(cashFundingRequestService.list).toHaveBeenCalledWith(1, 1, { status: "Pending" });
  });

  it("sin pendientes no se muestra", async () => {
    grant("caja.funding-requests.view");
    vi.mocked(cashFundingRequestService.list).mockResolvedValue({ items: [], pageNumber: 1, pageSize: 1, totalCount: 0 });
    renderNotice();
    await waitFor(() => expect(cashFundingRequestService.list).toHaveBeenCalled());
    expect(screen.queryByText(/Solicitudes de efectivo/)).toBeNull();
  });

  it("sin permiso view no consulta ni muestra nada", () => {
    grant("caja.view");
    renderNotice();
    expect(cashFundingRequestService.list).not.toHaveBeenCalled();
    expect(screen.queryByText(/Solicitudes de efectivo/)).toBeNull();
  });
});
