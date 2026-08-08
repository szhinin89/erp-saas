import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  lineGross,
  lineDiscountAmt,
  lineNet,
  calcLineTax,
  calcSummary,
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
