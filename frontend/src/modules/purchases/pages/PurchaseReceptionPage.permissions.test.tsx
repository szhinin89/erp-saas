// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseReceptionPage } from "./PurchaseReceptionPage";
import { usePermissionsUi } from "../../../access/usePermissionsUi";

// PURCHASES-RECEPTION-WRITE-PERMISSIONS-01 (H3) — la carga del TXT solo se ofrece con purchases.create.
vi.mock("../api/purchaseReceptionService", () => ({
  purchaseReceptionService: {
    importTxt: vi.fn(),
    downloadXml: vi.fn(),
    downloadXmlPending: vi.fn(),
    getXmlView: vi.fn(),
    refreshStatus: vi.fn(),
  },
}));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn(), confirm: vi.fn() },
}));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));

afterEach(cleanup);

function renderWith(granted: string[]) {
  const has = (key: string) => granted.includes(key);
  vi.mocked(usePermissionsUi).mockReturnValue({ canShow: has, has, isAdminRole: false });
  return render(
    <MemoryRouter>
      <I18nProvider>
        <PurchaseReceptionPage />
      </I18nProvider>
    </MemoryRouter>,
  );
}

describe("Recepción electrónica — importación según permiso", () => {
  it("un perfil de solo consulta ve el aviso y no la carga del TXT", () => {
    const view = renderWith(["purchases.view"]);
    expect(view.container.querySelector('input[type="file"]')).toBeNull();
    expect(screen.getByText("Su perfil solo permite consultar la recepción electrónica.")).toBeTruthy();
  });

  it("con purchases.create se ofrece la carga del TXT", () => {
    const view = renderWith(["purchases.view", "purchases.create"]);
    expect(view.container.querySelector('input[type="file"]')).not.toBeNull();
    expect(screen.queryByText("Su perfil solo permite consultar la recepción electrónica.")).toBeNull();
  });
});
