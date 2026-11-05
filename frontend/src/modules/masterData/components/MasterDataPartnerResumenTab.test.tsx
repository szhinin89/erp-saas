// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { MasterDataPartnerResumenTab } from "./MasterDataPartnerResumenTab";
import { useMasterDataCustomersUiStore, useMasterDataSuppliersUiStore } from "../store/masterDataPartnerUiStore";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";

const partners = [
  { id: "1", legalName: "Proveedor A", isActive: true },
  { id: "2", legalName: "Proveedor B", isActive: true },
  { id: "3", legalName: "Proveedor C", isActive: false },
] as BusinessPartnerSummaryDto[];

function renderSummary() {
  return render(
    <I18nProvider>
      <MasterDataPartnerResumenTab role="supplier" partners={partners}
        totalCount={120} store={useMasterDataSuppliersUiStore} />
    </I18nProvider>,
  );
}

beforeEach(() => {
  useMasterDataSuppliersUiStore.getState().reset();
  useMasterDataCustomersUiStore.getState().reset();
});
afterEach(cleanup);

describe("Resumen compacto de proveedores", () => {
  it("conserva el total del servidor y calcula activos/inactivos solo de la página", () => {
    renderSummary();
    for (const [label, value] of [
      ["Total registrados", "120"],
      ["Activos (página actual)", "2"],
      ["Inactivos (página actual)", "1"],
    ]) {
      expect(screen.getByText(label).closest(".pg-kpi")?.querySelector(".pg-kpi-value")?.textContent).toBe(value);
    }
  });

  it("compone las variantes oficiales sin panel lateral ni accesos duplicados", () => {
    const { container } = renderSummary();
    expect(container.querySelectorAll(".pg-kpi--h")).toHaveLength(3);
    expect(container.querySelector(".md-supplier-summary__activity.card")).toBeTruthy();
    expect(container.querySelector(".prd-quick-card")).toBeNull();
    expect(screen.queryAllByRole("button")).toHaveLength(0);
    expect(screen.getByRole("heading", { name: "Actividad reciente" })).toBeTruthy();
  });

  it("permanece exclusivamente de consulta y no cambia la pestaña del store", () => {
    renderSummary();
    expect(screen.queryByRole("button", { name: "Nuevo proveedor" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Ver listado" })).toBeNull();
    expect(useMasterDataSuppliersUiStore.getState().activeTab).toBe("resumen");
  });

  it("conserva el estado vacío sin inventar actividad", () => {
    renderSummary();
    expect(screen.getByText("Aún no hay acciones en esta sesión.")).toBeTruthy();
    expect(useMasterDataSuppliersUiStore.getState().recentActivity).toEqual([]);
  });

  it("muestra los eventos existentes, en su orden, sin alterar su origen", () => {
    const events = [
      { id: "a", partnerName: "Proveedor de nombre largo ".repeat(8).trim(), action: "updated" as const, timestamp: new Date() },
      { id: "b", partnerName: "Proveedor anterior", action: "disabled" as const, timestamp: new Date(Date.now() - 120000) },
    ];
    useMasterDataSuppliersUiStore.setState({ recentActivity: events });
    const { container } = renderSummary();
    expect(screen.queryByText("Aún no hay acciones en esta sesión.")).toBeNull();
    expect(Array.from(container.querySelectorAll(".prd-activity__name"), (node) => node.textContent)).toEqual(events.map(e => e.partnerName));
    expect(useMasterDataSuppliersUiStore.getState().recentActivity).toBe(events);
  });

  it("mantiene los tres indicadores en cero cuando no hay proveedores", () => {
    const { container } = render(<I18nProvider>
      <MasterDataPartnerResumenTab role="supplier" partners={[]} totalCount={0} store={useMasterDataSuppliersUiStore} />
    </I18nProvider>);
    expect(Array.from(container.querySelectorAll(".pg-kpi-value"), node => node.textContent)).toEqual(["0", "0", "0"]);
    expect(screen.queryByRole("button", { name: "Nuevo proveedor" })).toBeNull();
  });
});

it("Clientes conserva tarjetas verticales, panel y acciones anteriores", () => {
  const { container } = render(<I18nProvider>
    <MasterDataPartnerResumenTab role="customer" partners={partners} totalCount={120}
      store={useMasterDataCustomersUiStore} />
  </I18nProvider>);
  expect(container.querySelector(".md-supplier-summary")).toBeNull();
  expect(container.querySelectorAll(".pg-kpi--h")).toHaveLength(0);
  expect(container.querySelector(".prd-quick-card")).toBeTruthy();
  expect(container.querySelector(".prd-activity-card")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: /Nuevo cliente/ }));
  expect(useMasterDataCustomersUiStore.getState().activeTab).toBe("nuevo");
});
