// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { MasterDataPartnerListTab } from "./MasterDataPartnerListTab";
import { useMasterDataCustomersUiStore } from "../store/masterDataPartnerUiStore";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";

/**
 * ZH-LISTING-COMPLIANCE-AUDIT-08 (pendiente corregido) — MasterDataPartnerListTab debe calcular
 * rowNumberOffset = (page - 1) * pageSize para que la columna "N°" continúe la numeración en
 * páginas posteriores en lugar de reiniciar en 1.
 */

function buildPartner(id: string, name: string): BusinessPartnerSummaryDto {
  return {
    id,
    identificationType: "05",
    identificationNumber: `099999${id}`,
    legalName: name,
    tradeName: null,
    legalEntityTypeCode: 1,
    countryCode: "EC",
    isActive: true,
    createdAt: "2026-08-01T00:00:00Z",
    isCustomer: false,
    isSupplier: false,
    canAssignAsCustomer: true,
    canAssignAsSupplier: true,
  };
}

function renderTab(page: number, pageSize: number, role: "customer" | "supplier" = "customer", partner = buildPartner("1", "Cliente Página")) {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <MasterDataPartnerListTab
          role={role}
          store={useMasterDataCustomersUiStore}
          canCreate
          canUpdate
          canDisable
          canConfigure
          loading={false}
          saving={false}
          partners={[partner]}
          totalCount={120}
          search=""
          setSearch={() => {}}
          statusFilter="all"
          setStatusFilter={() => {}}
          page={page}
          pageSize={pageSize}
          totalPages={3}
          setPage={() => {}}
          onActivate={() => {}}
          onDisable={() => {}}
        />
      </MemoryRouter>
    </I18nProvider>,
  );
}

afterEach(() => cleanup());

describe("MasterDataPartnerListTab — showRowNumber con rowNumberOffset", () => {
  it("en la página 1, la primera fila muestra 1", () => {
    renderTab(1, 50);

    const rows = screen.getAllByRole("row").slice(1);
    const firstCell = within(rows[0]).getAllByRole("cell")[0];
    expect(firstCell.textContent).toBe("1");
  });

  it("en la página 3 con pageSize 50, la numeración continúa desde 101 (no reinicia en 1)", () => {
    renderTab(3, 50);

    const rows = screen.getAllByRole("row").slice(1);
    const firstCell = within(rows[0]).getAllByRole("cell")[0];
    expect(firstCell.textContent).toBe("101");
  });
});

describe("supplier identity", () => {
  it.each([null, "", "   ", "  Legal  "])("omits empty or identical trade name: %s", (tradeName) => {
    renderTab(1, 50, "supplier", {...buildPartner("1", "Legal"), tradeName});
    const cell = within(screen.getAllByRole("row")[1]).getAllByRole("cell")[2];
    expect(cell.querySelector(".md-supplier-identity__legal")?.textContent).toBe("Legal");
    expect(cell.querySelector(".md-supplier-identity__trade")).toBeNull();
  });
  it("shows full long legal name before distinct trade name", () => {
    const legalName = "Distribuidora de Suministros Industriales ".repeat(8).trim();
    renderTab(1, 50, "supplier", {...buildPartner("1", legalName), tradeName: "  Comercial  "});
    expect(screen.getByText(legalName).className).toBe("md-supplier-identity__legal");
    expect(screen.getByText("Comercial").className).toBe("md-supplier-identity__trade");
    expect(screen.getAllByText("0999991")).toHaveLength(2);
  });
  it("preserves customer identity and excludes supplier styles", () => {
    const {container} = renderTab(1, 50, "customer", {...buildPartner("1", "Legal"), tradeName: "Comercial"});
    const cell = within(screen.getAllByRole("row")[1]).getAllByRole("cell")[2];
    expect(cell.firstElementChild?.textContent).toBe("Comercial");
    expect(cell.lastElementChild?.textContent).toBe("Legal");
    expect(container.querySelector(".md-supplier-list")).toBeNull();
  });
});

// FUNC-PARTNER-ROLE-01 — «+ Proveedor» en Clientes respeta canAssignAsSupplier del backend.
describe("customer assign-as-supplier availability", () => {
  function renderCustomerActions(partner: BusinessPartnerSummaryDto, canUpdate = true) {
    const onAddAsSupplier = vi.fn();
    const onDisable = vi.fn();
    const onActivate = vi.fn();
    render(
      <I18nProvider>
        <MemoryRouter>
          <MasterDataPartnerListTab
            role="customer" store={useMasterDataCustomersUiStore}
            canCreate canUpdate={canUpdate} canDisable canConfigure
            loading={false} saving={false} partners={[partner]} totalCount={1}
            search="" setSearch={() => {}} statusFilter="all" setStatusFilter={() => {}}
            page={1} pageSize={50} totalPages={1} setPage={() => {}}
            onAddAsSupplier={onAddAsSupplier} onActivate={onActivate} onDisable={onDisable}
          />
        </MemoryRouter>
      </I18nProvider>,
    );
    return { onAddAsSupplier, onDisable, onActivate };
  }
  const assignButton = () => screen.queryByTitle("Agregar también como proveedor");

  it("shows it for an active customer without supplier role and keeps the callback", () => {
    const { onAddAsSupplier } = renderCustomerActions(buildPartner("1", "Cliente"));
    fireEvent.click(assignButton()!);
    expect(onAddAsSupplier).toHaveBeenCalledExactlyOnceWith("1");
  });
  it("hides it when the customer already has the supplier role; other actions remain", () => {
    const { onDisable } = renderCustomerActions({ ...buildPartner("1", "Cliente"), isSupplier: true, canAssignAsSupplier: false });
    expect(assignButton()).toBeNull();
    expect(screen.getByRole("button", { name: "Editar" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Deshabilitar" }));
    expect(onDisable).toHaveBeenCalledExactlyOnceWith("1");
  });
  it("hides it for an inactive customer while activation remains", () => {
    const { onActivate } = renderCustomerActions({ ...buildPartner("1", "Cliente"), isActive: false, canAssignAsSupplier: false });
    expect(assignButton()).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Habilitar" }));
    expect(onActivate).toHaveBeenCalledExactlyOnceWith("1");
  });
  it("hides it without update permission even if assignable", () => {
    renderCustomerActions(buildPartner("1", "Cliente"), false);
    expect(assignButton()).toBeNull();
  });
});
