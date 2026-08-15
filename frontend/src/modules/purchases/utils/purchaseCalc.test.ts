import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  lineGross,
  lineDiscountAmt,
  lineNet,
  calcLineTax,
  calcSummary,
  calcVatBreakdown,
  generateScheduleRows,
} from "./purchaseCalc";
import type { PurchaseItemContextDto } from "../api/purchaseService";

// Los tests solo ejercitan vatPercent/icePercent (los únicos campos que lee
// calcLineTax); el resto del contrato PurchaseItemContextDto es irrelevante aquí.
const mkLine = (
  qty: number,
  price: number,
  vatCode = "10",
  discountPct = 0,
  ctx?: Partial<PurchaseItemContextDto>,
) => ({
  description: "Test",
  quantity: qty,
  unitPrice: price,
  vatCode,
  discountPct,
  ...(ctx && { context: ctx as PurchaseItemContextDto }),
});

// ────────────────────────────────────────────────────────
// 1. Compra contado — sin descuento, IVA 15%
// ────────────────────────────────────────────────────────
describe("compra contado — IVA 15%, sin descuento", () => {
  const lines = [
    mkLine(10, 100, "10", 0, { vatPercent: 15, icePercent: 0 }),
    mkLine(5, 50, "10", 0, { vatPercent: 15, icePercent: 0 }),
  ];

  it("subtotal bruto", () => {
    expect(lines.reduce((s, l) => s + lineGross(l), 0)).toBe(1250);
  });

  it("descuento cero", () => {
    expect(lines.reduce((s, l) => s + lineDiscountAmt(l), 0)).toBe(0);
  });

  it("IVA 15% sobre neto", () => {
    const vat = lines.reduce((s, l) => s + calcLineTax(l).vat, 0);
    expect(vat).toBe(187.5);
  });

  it("total incluye IVA", () => {
    const s = calcSummary(lines, 0, 0);
    expect(s.total).toBe(1437.5);
  });
});

// ────────────────────────────────────────────────────────
// 2. Compra crédito — con flete y otros costos
// ────────────────────────────────────────────────────────
describe("compra crédito — flete + otros costos", () => {
  const lines = [mkLine(10, 100, "10", 0, { vatPercent: 15, icePercent: 0 })];

  it("total incluye flete y otros", () => {
    const s = calcSummary(lines, 25, 10);
    expect(s.total).toBe(1000 + 150 + 25 + 10);
  });
});

// ────────────────────────────────────────────────────────
// 3. Compra con IVA 15%
// ────────────────────────────────────────────────────────
describe("IVA 15% — con contexto", () => {
  it("calcula IVA desde contexto", () => {
    const l = mkLine(1, 200, "10", 0, { vatPercent: 15, icePercent: 0 });
    expect(calcLineTax(l).vat).toBe(30);
  });

  it("sin contexto ni catálogo, el IVA es 0 (nunca se infiere del código)", () => {
    const l = mkLine(1, 200, "10");
    expect(calcLineTax(l).vat).toBe(0);
  });

  it("sin contexto, usa el catálogo SRI (vatRates) por código", () => {
    const l = mkLine(1, 200, "10");
    expect(calcLineTax(l, { "10": 15 }).vat).toBe(30);
  });
});

// ────────────────────────────────────────────────────────
// 4. Compra IVA 0%
// ────────────────────────────────────────────────────────
describe("IVA 0%", () => {
  it("vatCode 0 → IVA cero con contexto", () => {
    const l = mkLine(1, 100, "0", 0, { vatPercent: 0, icePercent: 0 });
    expect(calcLineTax(l).vat).toBe(0);
  });

  it("vatCode 0 → IVA cero sin contexto ni catálogo", () => {
    const l = mkLine(1, 100, "0");
    expect(calcLineTax(l).vat).toBe(0);
  });

  it("vatCode 20 → IVA cero sin contexto ni catálogo (nunca se infiere)", () => {
    const l = mkLine(1, 100, "20");
    expect(calcLineTax(l).vat).toBe(0);
  });

  it("vatCode 20 → usa el catálogo SRI (vatRates) por código", () => {
    const l = mkLine(1, 100, "20");
    expect(calcLineTax(l, { "20": 5 }).vat).toBe(5);
  });
});

// ────────────────────────────────────────────────────────
// 5. Compra con descuento
// ────────────────────────────────────────────────────────
describe("descuento por línea", () => {
  it("10% descuento sobre bruto", () => {
    const l = mkLine(10, 100, "10", 10);
    expect(lineGross(l)).toBe(1000);
    expect(lineDiscountAmt(l)).toBe(100);
    expect(lineNet(l)).toBe(900);
  });

  it("IVA se calcula sobre neto (post-descuento)", () => {
    const l = mkLine(10, 100, "10", 10, { vatPercent: 15, icePercent: 0 });
    expect(calcLineTax(l).vat).toBe(135);
  });

  it("summary con descuento mixto", () => {
    const lines = [
      mkLine(10, 100, "10", 10, { vatPercent: 15, icePercent: 0 }),
      mkLine(5, 50, "10", 0, { vatPercent: 15, icePercent: 0 }),
    ];
    const s = calcSummary(lines, 0, 0);
    expect(s.subtotal).toBe(1250);
    expect(s.discount).toBe(100);
    expect(s.netSubtotal).toBe(1150);
    expect(s.vat).toBe(172.5);
    expect(s.total).toBe(1322.5);
  });
});

// ────────────────────────────────────────────────────────
// 6. Compra con flete
// ────────────────────────────────────────────────────────
describe("flete", () => {
  it("flete se suma al total", () => {
    const s = calcSummary(
      [mkLine(1, 100, "0", 0, { vatPercent: 0, icePercent: 0 })],
      50,
      0,
    );
    expect(s.total).toBe(150);
  });
});

// ────────────────────────────────────────────────────────
// 7. Compra con otros costos
// ────────────────────────────────────────────────────────
describe("otros costos", () => {
  it("otros costos se suman al total", () => {
    const s = calcSummary(
      [mkLine(1, 100, "0", 0, { vatPercent: 0, icePercent: 0 })],
      0,
      30,
    );
    expect(s.total).toBe(130);
  });
});

// ────────────────────────────────────────────────────────
// 8. Compra con múltiples líneas
// ────────────────────────────────────────────────────────
describe("múltiples líneas mixtas", () => {
  const lines = [
    mkLine(10, 100, "10", 5, { vatPercent: 15, icePercent: 0 }),
    mkLine(3, 200, "10", 0, { vatPercent: 15, icePercent: 0 }),
    mkLine(20, 10, "0", 0, { vatPercent: 0, icePercent: 0 }),
    mkLine(1, 500, "10", 10, { vatPercent: 15, icePercent: 2 }),
  ];

  it("subtotal bruto", () => {
    const s = calcSummary(lines, 0, 0);
    expect(s.subtotal).toBe(10 * 100 + 3 * 200 + 20 * 10 + 1 * 500);
  });

  it("descuento total", () => {
    const s = calcSummary(lines, 0, 0);
    expect(s.discount).toBe(1000 * 0.05 + 500 * 0.1);
  });

  it("IVA solo sobre líneas con IVA (neto)", () => {
    const s = calcSummary(lines, 0, 0);
    const expectedVat = 950 * 0.15 + 600 * 0.15 + 0 + 450 * 0.15;
    expect(s.vat).toBe(expectedVat);
  });

  it("ICE solo sobre línea con ICE", () => {
    const s = calcSummary(lines, 0, 0);
    expect(s.ice).toBe(450 * 0.02);
  });
});

// ────────────────────────────────────────────────────────
// 8a. PURCHASE-INVOICE-TOTALS-SUMMARY-PANEL-01 — desglose por tarifa IVA (panel "Totales")
// ────────────────────────────────────────────────────────
describe("calcVatBreakdown — agrupación por tarifa IVA", () => {
  it("agrupa dos líneas con la misma tarifa 15% en una sola fila", () => {
    const lines = [
      mkLine(10, 100, "10", 10, { vatPercent: 15, icePercent: 0 }),
      mkLine(5, 50, "10", 0, { vatPercent: 15, icePercent: 0 }),
    ];
    const rows = calcVatBreakdown(lines);
    expect(rows).toHaveLength(1);
    expect(rows[0].vatPercent).toBe(15);
    expect(rows[0].taxableBase).toBe(1150);
    expect(rows[0].discount).toBe(100);
    expect(rows[0].vat).toBe(172.5);
    expect(rows[0].ice).toBe(0);
    expect(rows[0].total).toBe(1322.5);
  });

  it("separa filas distintas por tarifa IVA (15% vs 0%), con ICE/IRBPNR opcional por línea", () => {
    const lines = [
      mkLine(10, 100, "10", 5, { vatPercent: 15, icePercent: 0 }),
      mkLine(20, 10, "0", 0, { vatPercent: 0, icePercent: 0 }),
      mkLine(1, 500, "10", 10, { vatPercent: 15, icePercent: 2 }),
    ];
    const rows = calcVatBreakdown(lines);
    expect(rows).toHaveLength(2);
    // Orden descendente por tarifa: 15% primero, luego 0%.
    expect(rows[0].vatPercent).toBe(15);
    expect(rows[0].taxableBase).toBe(950 + 450);
    expect(rows[0].vat).toBe(950 * 0.15 + 450 * 0.15);
    expect(rows[0].ice).toBe(450 * 0.02);
    expect(rows[0].total).toBeCloseTo(950 + 950 * 0.15 + 450 + 450 * 0.15 + 450 * 0.02, 6);
    expect(rows[1].vatPercent).toBe(0);
    expect(rows[1].taxableBase).toBe(200);
    expect(rows[1].vat).toBe(0);
    expect(rows[1].total).toBe(200);
  });

  it("no usa campos xml* — el desglose solo lee quantity/unitPrice/discountPct/vatCode/iceCode del draft", () => {
    const l = {
      ...mkLine(2, 50, "10", 0, { vatPercent: 15, icePercent: 0 }),
      xmlQuantity: 999,
      xmlUnitPrice: 999,
      xmlTaxableBase: 999,
      xmlTaxValue: 999,
      xmlTotalLine: 999,
    };
    const rows = calcVatBreakdown([l]);
    expect(rows).toHaveLength(1);
    expect(rows[0].taxableBase).toBe(100);
    expect(rows[0].vat).toBe(15);
    expect(rows[0].total).toBe(115);
  });

  it("lista vacía retorna sin filas", () => {
    expect(calcVatBreakdown([])).toEqual([]);
  });
});

// ────────────────────────────────────────────────────────
// 8b. PURCHASE-LINE-HEADER-TAXABLE-BASE-ORDER-01 — Base imponible de cabecera
//     (8 SIXPACK / 48 unidades, mismo caso usado en purchaseLinePresentation.test.ts)
// ────────────────────────────────────────────────────────
describe("Base imponible de cabecera — caso 8 SIXPACK / 48 unidades", () => {
  const l = mkLine(8, 5.9125, "10", (6.43 / 47.3) * 100, {
    vatPercent: 15,
    icePercent: 0,
  });

  it("base imponible (lineNet) ≈ 40.87 — subtotal bruto menos descuento", () => {
    expect(lineGross(l)).toBeCloseTo(47.3, 2);
    expect(lineDiscountAmt(l)).toBeCloseTo(6.43, 2);
    expect(lineNet(l)).toBeCloseTo(40.87, 2);
  });

  it("IVA sobre la base imponible ≈ 6.13 y total (base + IVA + ICE) ≈ 47.00", () => {
    const { vat, ice } = calcLineTax(l);
    expect(vat).toBeCloseTo(6.13, 2);
    expect(ice).toBe(0);
    expect(lineNet(l) + vat + ice).toBeCloseTo(47.0, 2);
  });
});

// ────────────────────────────────────────────────────────
// 8c. PURCHASE-LINE-HEADER-EDIT-RECALCULATE-TAX-TOTAL-01 — tras editar Cantidad/Costo en
//     cabecera, la misma línea (CLUB PLATINO) debe recalcular IVA/Total, no arrastrar el
//     snapshot XML (40.87/6.13/47.00) que probó el bloque anterior.
// ────────────────────────────────────────────────────────
describe("Recálculo de IVA/Total al editar Cantidad/Costo — base 100, IVA 15%", () => {
  const edited = mkLine(50, 2, "10", 0, { vatPercent: 15, icePercent: 0 });

  it("base 100, vatRate 15 => vat 15, total 115", () => {
    expect(lineNet(edited)).toBe(100);
    const { vat, ice } = calcLineTax(edited);
    expect(vat).toBe(15);
    expect(ice).toBe(0);
    expect(lineNet(edited) + vat + ice).toBe(115);
  });

  it("editar solo Cantidad (50 -> 100) recalcula IVA y Total proporcionalmente", () => {
    const l2 = mkLine(100, 2, "10", 0, { vatPercent: 15, icePercent: 0 });
    expect(lineNet(l2)).toBe(200);
    expect(calcLineTax(l2).vat).toBe(30);
    expect(lineNet(l2) + calcLineTax(l2).vat).toBe(230);
  });

  it("editar solo Costo (2 -> 3) recalcula IVA y Total proporcionalmente", () => {
    const l3 = mkLine(50, 3, "10", 0, { vatPercent: 15, icePercent: 0 });
    expect(lineNet(l3)).toBe(150);
    expect(calcLineTax(l3).vat).toBe(22.5);
    expect(lineNet(l3) + calcLineTax(l3).vat).toBe(172.5);
  });
});

// ────────────────────────────────────────────────────────
// 9. Cuotas — generación
// ────────────────────────────────────────────────────────
describe("generación de cuotas", () => {
  const originalTz = process.env.TZ;

  beforeAll(() => {
    process.env.TZ = "Asia/Tokyo";
  });

  afterAll(() => {
    process.env.TZ = originalTz;
  });

  it("1 cuota contado", () => {
    const rows = generateScheduleRows(1, 0, 1000, "2026-06-01");
    expect(rows).toHaveLength(1);
    expect(rows[0].amount).toBe(1000);
    expect(rows[0].dueDate).toBe("2026-06-01");
  });

  it("3 cuotas iguales cada 30 días", () => {
    const rows = generateScheduleRows(3, 30, 900, "2026-01-01");
    expect(rows).toHaveLength(3);
    expect(rows[0].amount).toBe(300);
    expect(rows[1].amount).toBe(300);
    expect(rows[2].amount).toBe(300);
    expect(rows[0].dueDate).toBe("2026-01-31");
    expect(rows[1].dueDate).toBe("2026-03-02");
    expect(rows[2].dueDate).toBe("2026-04-01");
  });

  it("última cuota absorbe residuo de redondeo", () => {
    const rows = generateScheduleRows(3, 30, 1000, "2026-01-01");
    expect(rows[0].amount).toBe(333.33);
    expect(rows[1].amount).toBe(333.33);
    expect(rows[2].amount).toBe(333.34);
    const sum = rows.reduce((s, r) => s + r.amount, 0);
    expect(Math.round(sum * 100)).toBe(100000);
  });

  it("retorna vacío si count < 1", () => {
    expect(generateScheduleRows(0, 30, 1000, "2026-01-01")).toEqual([]);
  });

  it("retorna vacío si date vacío", () => {
    expect(generateScheduleRows(3, 30, 1000, "")).toEqual([]);
  });

  it("total 0 genera cuotas con monto 0", () => {
    const rows = generateScheduleRows(2, 30, 0, "2026-01-01");
    expect(rows).toHaveLength(2);
    expect(rows[0].amount).toBe(0);
    expect(rows[1].amount).toBe(0);
  });

  it("mantiene el día calendario local de la fecha base", () => {
    const rows = generateScheduleRows(1, 0, 100, "2026-07-13");
    expect(rows[0].dueDate).toBe("2026-07-13");
  });
});

// ────────────────────────────────────────────────────────
// 10. Confirmación — payload de cuotas
// ────────────────────────────────────────────────────────
describe("payload de confirmación", () => {
  it("cuotas suman exactamente el total", () => {
    const total = 1897.5;
    const rows = generateScheduleRows(3, 30, total, "2026-06-01");
    const payloadSum = rows.reduce((s, r) => s + r.amount, 0);
    expect(Math.round(payloadSum * 100)).toBe(Math.round(total * 100));
  });

  it("números de cuota son secuenciales desde 1", () => {
    const rows = generateScheduleRows(5, 15, 500, "2026-01-01");
    rows.forEach((r, i) => expect(r.number).toBe(i + 1));
  });
});
