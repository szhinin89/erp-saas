// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { RetentionSection } from "./PurchasesPage";

// PURCHASE-PAYABLE-E2E-01 — una compra confirmada sin retención mostraba la clave i18n cruda
// "purchases.retention.noneIssued" en lugar del texto.

type Ctx = Parameters<typeof RetentionSection>[0]["ctx"];

afterEach(cleanup);

describe("RetentionSection — compra confirmada", () => {
  it("sin retención emitida muestra el texto traducido", () => {
    const ctx = {
      editing: { id: "p-1", status: "Confirmed" },
      retention: null,
      electronicPending: false,
      handleViewRetentionXml: vi.fn(),
      handleViewRetentionRidePdf: vi.fn(),
    } as unknown as Ctx;

    render(
      <MemoryRouter>
        <I18nProvider>
          <RetentionSection ctx={ctx} />
        </I18nProvider>
      </MemoryRouter>,
    );

    expect(screen.getByText("No se emitió retención para esta compra.")).toBeTruthy();
    expect(screen.queryByText("purchases.retention.noneIssued")).toBeNull();
  });
});
