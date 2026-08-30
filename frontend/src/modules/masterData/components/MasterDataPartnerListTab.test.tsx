// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
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
  };
}

function renderTab(page: number, pageSize: number) {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <MasterDataPartnerListTab
          role="customer"
          store={useMasterDataCustomersUiStore}
          canCreate
          canUpdate
          canDisable
          canConfigure
          loading={false}
          saving={false}
          partners={[buildPartner("1", "Cliente Página")]}
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
