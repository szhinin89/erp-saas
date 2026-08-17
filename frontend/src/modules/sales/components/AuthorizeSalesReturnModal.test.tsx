// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { AuthorizeSalesReturnModal } from "./AuthorizeSalesReturnModal";
import type { SalesReturnDto } from "../api/salesReturnService";
import { I18nProvider } from "../../../i18n/i18n";

vi.mock("../api/salesReturnService", async () => {
  const actual = await vi.importActual<
    typeof import("../api/salesReturnService")
  >("../api/salesReturnService");
  return {
    ...actual,
    salesReturnService: {
      authorize: vi.fn(),
    },
  };
});

// SALES-DS-MONEY-12 — "Asignado" y "Diferencia" del resumen de asignación de
// reembolso migraron de texto plano a ZHMoneyValue (currencySymbol="" para
// conservar el texto visible exacto: nunca mostraron "$"). El subtitle del
// modal (texto compuesto con el número de devolución) se mantiene sin migrar.

function buildSalesReturn(overrides: Partial<SalesReturnDto> = {}): SalesReturnDto {
  return {
    id: "sr-1",
    salesInvoiceId: "inv-1",
    customerId: "cust-1",
    returnNumber: "DEV-001",
    reason: "Producto defectuoso",
    status: "Draft",
    subtotal: 100,
    totalVat: 15,
    totalIce: 0,
    totalDiscount: 0,
    grandTotal: 115,
    lines: [],
    refundAllocations: [],
    createdAt: "2026-07-01T00:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

function renderModal(salesReturn: SalesReturnDto) {
  return render(
    <I18nProvider>
      <AuthorizeSalesReturnModal
        open
        salesReturn={salesReturn}
        onClose={vi.fn()}
        onAuthorized={vi.fn()}
      />
    </I18nProvider>,
  );
}

afterEach(() => {
  cleanup();
});

describe("AuthorizeSalesReturnModal — resumen de asignación migrado a ZHMoneyValue (SALES-DS-MONEY-12)", () => {
  it('"Asignado" usa ZHMoneyValue sin símbolo de moneda (igual que el texto plano anterior)', () => {
    const { container } = renderModal(buildSalesReturn({ grandTotal: 115 }));

    const summary = container.querySelector(".sr-refund-summary");
    const moneyValue = summary?.querySelector(".zh-money-value");
    expect(moneyValue).toBeTruthy();
    // El default inicial asigna el 100% del total (Cash) a la primera fila.
    expect(moneyValue?.textContent).toBe("115.00");
  });

  it('"Coincide con el total" se muestra cuando la asignación cubre el total exacto', () => {
    renderModal(buildSalesReturn({ grandTotal: 115 }));

    expect(screen.getByText("Coincide con el total")).toBeTruthy();
  });

  it("el input de monto por asignación sigue siendo editable, no ZHMoneyValue", () => {
    renderModal(buildSalesReturn({ grandTotal: 115 }));

    const input = screen.getByDisplayValue("115");
    expect(input.tagName).toBe("INPUT");
  });

  it("reducir el monto asignado muestra Diferencia vía ZHMoneyValue", () => {
    const { container } = renderModal(buildSalesReturn({ grandTotal: 115 }));

    const input = screen.getByDisplayValue("115");
    fireEvent.change(input, { target: { value: "50" } });

    const summary = container.querySelector(".sr-refund-summary");
    const pendingSpan = summary?.querySelector(".sr-refund-summary--pending");
    expect(pendingSpan).toBeTruthy();
    expect(pendingSpan?.querySelector(".zh-money-value")).toBeTruthy();
    expect(pendingSpan?.textContent).toContain("Diferencia:");
    expect(pendingSpan?.querySelector(".zh-money-value")?.textContent).toBe(
      "65.00",
    );
  });

  it("no hay estilos inline en el resumen de asignación", () => {
    const { container } = renderModal(buildSalesReturn({ grandTotal: 115 }));

    container
      .querySelectorAll(".sr-refund-summary .zh-money-value")
      .forEach((el) => {
        expect(el.getAttribute("style")).toBeNull();
      });
  });
});
