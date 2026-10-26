// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { KardexMovementDetailModal } from "./KardexMovementDetailModal";
import type { KardexMovementDetailDto } from "../../stock/api/kardexService";
import { setPrecisionPolicyForTests } from "../../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../../test/precisionPolicyFixture";

afterEach(() => cleanup());

const current = { movementId: "m-1", sequenceNumber: 1, movementTypeName: "PositiveAdjust", effectiveDate: "2026-09-01" };

const detail: KardexMovementDetailDto = {
  movement: {
    id: "m-1",
    itemId: "i-1",
    warehouseId: "w-1",
    movementType: 1,
    movementTypeName: "PositiveAdjust",
    quantity: 3,
    uomCode: "UND",
    previousQuantity: 1,
    resultQuantity: 4,
    sequenceNumber: 1,
    unitCost: 1.23456789,
    totalCost: 3.7,
    runningAverageCost: 0.4285714286,
    runningStockValue: 3,
    effectiveDate: "2026-09-01",
    reference: null,
    sourceDocId: null,
    sourceDocType: null,
    createdBy: "u-1",
    createdAt: "2026-09-01T00:00:00Z",
    createdByName: null,
  },
  sourceDocument: null,
  actor: { userId: "u-1", userName: "Admin" },
  documentChain: { links: [] },
  relations: { current, previous: null, next: null },
};

function renderModal(modalDetail = detail) {
  const { container } = render(
    <MemoryRouter>
      <KardexMovementDetailModal
        open
        loading={false}
        detail={modalDetail}
        onClose={vi.fn()}
        onNavigate={vi.fn()}
        movementTypeLabels={{}}
      />
    </MemoryRouter>,
  );
  return container.ownerDocument.body.textContent ?? "";
}

// ERP-PRECISION-FRONTEND-06B: costo unitario → unitCostDecimals; costo promedio → averageCostDecimals.
describe("KardexMovementDetailModal — precisión de costos", () => {
  it("conserva navegación al documento, movimientos relacionados y cierre", () => {
    const onNavigate = vi.fn();
    const onClose = vi.fn();
    function Location() { return <output>{useLocation().search}</output>; }
    render(<MemoryRouter>
      <Location />
      <KardexMovementDetailModal open loading={false} detail={{
        ...detail,
        movement: { ...detail.movement, sourceDocId: "invoice-1", sourceDocType: "PurchaseInvoice" },
        sourceDocument: { docType: "PurchaseInvoice", docNumber: "001-001-000000001", partnerName: null,
          unitPrice: null, discountPct: null, vatCode: null, vatRate: null, reason: null, notes: null },
        relations: { current, previous: { ...current, movementId: "previous", sequenceNumber: 10 },
          next: { ...current, movementId: "next", sequenceNumber: 12 } },
      }} onNavigate={onNavigate} onClose={onClose} movementTypeLabels={{}} />
    </MemoryRouter>);
    fireEvent.click(screen.getByRole("button", { name: /Ver documento origen/ }));
    expect(screen.getByRole("status").textContent).toBe("?invoiceId=invoice-1");
    fireEvent.click(screen.getByRole("button", { name: /#10/ }));
    fireEvent.click(screen.getByRole("button", { name: /#12/ }));
    expect(onNavigate.mock.calls).toEqual([["previous"], ["next"]]);
    fireEvent.click(screen.getByRole("button", { name: "Cerrar" }));
    expect(onClose).toHaveBeenCalledOnce();
  });
  it("muestra cantidad a 6 y costos unitario/promedio a 10 sin recortar el DTO", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY,
      quantityDecimals: 6, unitCostDecimals: 10, averageCostDecimals: 10 });
    const text = renderModal({ ...detail, movement: { ...detail.movement,
      quantity: 1.123456, unitCost: 1.1234567891, runningAverageCost: 0.1234567891 } });
    expect(text).toContain("+1.123456 UND");
    expect(text).toContain("Costo Unitario$1.1234567891");
    expect(text).toContain("Costo Promedio Corrido$0.1234567891");
  });

  it.each([
    ["PurchaseInvoice", "$1.235"],
    ["SalesInvoice", "$1.23457"],
  ])("Precio Comercial de %s usa su precisión semántica", (docType, expected) => {
    setPrecisionPolicyForTests({
      ...TEST_PRECISION_POLICY,
      purchaseUnitPriceDecimals: 3,
      salesUnitPriceDecimals: 5,
    });
    const text = renderModal({
      ...detail,
      sourceDocument: { docType, docNumber: null, partnerName: null,
        unitPrice: 1.23456789, discountPct: null, vatCode: null,
        vatRate: null, reason: null, notes: null },
    });
    expect(text).toContain(`Precio Comercial${expected}`);
  });

  it.each(["StockAdjustment", "StockTransfer"])(
    "%s sin UnitPrice no muestra Precio Comercial", (docType) => {
      const text = renderModal({
        ...detail,
        sourceDocument: { docType, docNumber: null, partnerName: null,
          unitPrice: null, discountPct: null, vatCode: null,
          vatRate: null, reason: null, notes: null },
      });
      expect(text).not.toContain("Precio Comercial");
    },
  );

  it("Costo Unitario usa unitCostDecimals y Costo Promedio Corrido usa averageCostDecimals", () => {
    setPrecisionPolicyForTests({
      ...TEST_PRECISION_POLICY,
      unitCostDecimals: 8,
      averageCostDecimals: 10,
      purchaseUnitPriceDecimals: 3,
    });

    const text = renderModal();

    expect(text).toContain("$1.23456789");
    expect(text).toContain("$0.4285714286");
    // Totales siguen en moneyDecimals.
    expect(text).toContain("$3.70");
    expect(text).toContain("$3.00");
  });
});
