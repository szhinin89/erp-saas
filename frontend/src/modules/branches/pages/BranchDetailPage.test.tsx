// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { BranchDetailPage } from "./BranchDetailPage";
import { branchService, type BranchDetailDto } from "../api/branchService";

const denied = new Set<string>();
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: (perm: string) => !denied.has(perm) }),
}));
vi.mock("../api/branchService", () => ({ branchService: { getById: vi.fn() } }));
vi.mock("../components/BranchGeneralSection", () => ({
  BranchGeneralSection: () => <div>sección general</div>,
}));
vi.mock("../components/BranchConfigSection", () => ({
  BranchConfigSection: () => <div>sección configuraciones</div>,
}));

const BRANCH = { id: "b1", name: "Sucursal Centro", address: "Av. Principal 123" } as BranchDetailDto;

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  denied.clear();
});

const renderPage = () =>
  render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/settings/branches/b1"]}>
        <Routes>
          <Route path="/settings/branches/:id" element={<BranchDetailPage />} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );

describe("BranchDetailPage — pestañas (ZHTabBar, UIUX-GLOBAL-TABS-DEFAULT-01)", () => {
  it("usa ZHTabBar con tres pestañas y el patrón responsive automático", async () => {
    vi.mocked(branchService.getById).mockResolvedValue(BRANCH);
    renderPage();
    const tablist = await screen.findByRole("tablist", { name: "Secciones de la sucursal" });
    expect(tablist.classList.contains("prd-tabs--responsive-three")).toBe(true);
    expect(screen.getAllByRole("tab").map((t) => t.getAttribute("aria-label"))).toEqual([
      "General",
      "Establecimientos",
      "Configuraciones",
    ]);
  });

  it("etiquetas móviles «Establ.»/«Config.» con nombre accesible y etiqueta completa intactos", async () => {
    vi.mocked(branchService.getById).mockResolvedValue(BRANCH);
    renderPage();
    await screen.findByRole("tablist", { name: "Secciones de la sucursal" });
    for (const [name, short] of [["Establecimientos", "Establ."], ["Configuraciones", "Config."]]) {
      const tab = screen.getByRole("tab", { name });
      expect(tab.querySelector(".prd-tab-label--full")?.textContent).toBe(name);
      const mobile = tab.querySelector(".prd-tab-label--mobile");
      expect(mobile?.textContent).toBe(short);
      expect(mobile?.getAttribute("aria-hidden")).toBe("true");
    }
    const general = screen.getByRole("tab", { name: "General" });
    expect(general.querySelector(".prd-tab-label--mobile")).toBeNull();
  });

  it("conserva la navegación entre secciones y el estado activo", async () => {
    vi.mocked(branchService.getById).mockResolvedValue(BRANCH);
    renderPage();
    expect(await screen.findByText("sección general")).toBeTruthy();
    expect(screen.getByRole("tab", { name: "General" }).getAttribute("aria-selected")).toBe("true");

    fireEvent.click(screen.getByRole("tab", { name: "Configuraciones" }));
    expect(screen.getByText("sección configuraciones")).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Configuraciones" }).getAttribute("aria-selected")).toBe("true");

    fireEvent.click(screen.getByRole("tab", { name: "Establecimientos" }));
    expect(screen.getByText("Establecimientos SRI asociados")).toBeTruthy();
    expect(screen.queryByText("sección general")).toBeNull();
  });

  it("oculta Configuraciones sin settings.company.view (permiso del GET de la pestaña)", async () => {
    // BRANCH-CONFIG-READ-PERMISSION-01
    denied.add("settings.company.view");
    vi.mocked(branchService.getById).mockResolvedValue(BRANCH);
    renderPage();
    await screen.findByRole("tablist", { name: "Secciones de la sucursal" });
    expect(screen.getAllByRole("tab")).toHaveLength(2);
    expect(screen.getByRole("tab", { name: "General" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: /Establecimientos/ })).toBeTruthy();
    expect(screen.queryByRole("tab", { name: /Configuraciones/ })).toBeNull();
    expect(screen.queryByText("sección configuraciones")).toBeNull();
  });
});
