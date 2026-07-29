import { describe, it, expect } from "vitest";
import {
  lineGross,
  lineDiscountAmt,
  lineNet,
  calcLineTax,
  calcSummary,
} from "./salesCalc";
import type {
  SalesLineInput,
  SalesInvoiceDetailDto,
} from "../api/salesService";

// ── Mock data: deterministic, no business logic, numeric placeholders only ──

const LINES: Record<string, SalesLineInput> = {
  simple: {
    description: "Resma de papel A4",
    quantity: 10,
    unitPrice: 5.0,
    vatCode: "10",
    discountPct: 0,
  },
  withDiscount: {
    itemId: "item-002",
    description: "Toner HP 85A",
    quantity: 2,
    unitPrice: 45.0,
    vatCode: "10",
    discountPct: 10,
  },
  vatZero: {
    description: "Servicio de consultoría",
    quantity: 1,
    unitPrice: 200.0,
    vatCode: "0",
    discountPct: 0,
  },
  vat5pct: {
    description: "Producto tarifa reducida",
    quantity: 3,
    unitPrice: 30.0,
    vatCode: "20",
    discountPct: 0,
  },
  withIce: {
    description: "Bebida gaseosa 500ml",
    quantity: 24,
    unitPrice: 0.75,
    vatCode: "10",
    discountPct: 0,
    iceCode: "ICE01",
  },
};

const TEST_VAT_RATES: Record<string, number> = { "0": 0, "10": 15, "20": 5 };
const TEST_ICE_RATES: Record<string, number> = { ICE01: 10 }; // 10% ad valorem

export const mockBackendDetails: SalesInvoiceDetailDto[] = [
  {
    id: "det-001",
    itemId: "item-001",
    warehouseId: null,
    description: "Resma de papel A4",
    snapshotSku: "PAP-A4",
    snapshotItemName: "Resma Papel A4 75gr",
    uomCode: "UNIT",
    conversionFactor: 1,
    quantityInBaseUom: 10,
    quantity: 10,
    unitPrice: 5.0,
    discountPct: 0,
    discountAmount: 0,
    taxableBase: 50.0,
    vatCode: "10",
    vatRate: 15,
    vatAmount: 7.5,
    snapshotVatName: "IVA 15%",
    iceCode: null,
    iceRate: 0,
    iceAmount: 0,
    snapshotIceName: null,
    taxInclusiveTotal: 57.5,
    notes: null,
    sortOrder: 1,
  },
  {
    id: "det-002",
    itemId: "item-002",
    warehouseId: null,
    description: "Toner HP 85A",
    snapshotSku: "TON-85A",
    snapshotItemName: "Toner HP 85A Compatible",
    uomCode: "UNIT",
    conversionFactor: 1,
    quantityInBaseUom: 2,
    quantity: 2,
    unitPrice: 45.0,
    discountPct: 10,
    discountAmount: 9.0,
    taxableBase: 81.0,
    vatCode: "10",
    vatRate: 15,
    vatAmount: 12.15,
    snapshotVatName: "IVA 15%",
    iceCode: null,
    iceRate: 0,
    iceAmount: 0,
    snapshotIceName: null,
    taxInclusiveTotal: 93.15,
    notes: null,
    sortOrder: 2,
  },
  {
    id: "det-003",
    itemId: null,
    warehouseId: null,
    description: "Servicio de consultoría",
    snapshotSku: null,
    snapshotItemName: null,
    uomCode: "UNIT",
    conversionFactor: 1,
    quantityInBaseUom: 1,
    quantity: 1,
    unitPrice: 200.0,
    discountPct: 0,
    discountAmount: 0,
    taxableBase: 200.0,
    vatCode: "0",
    vatRate: 0,
    vatAmount: 0,
    snapshotVatName: "IVA 0%",
    iceCode: null,
    iceRate: 0,
    iceAmount: 0,
    snapshotIceName: null,
    taxInclusiveTotal: 200.0,
    notes: null,
    sortOrder: 3,
  },
];

// ── Tests: frontend preview approximation (NOT fiscal authority) ──

describe("salesCalc — UI preview layer", () => {
  describe("lineGross", () => {
    it("multiplies quantity by unit price", () => {
      expect(lineGross(LINES.simple)).toBe(50);
    });

    it("handles fractional quantities", () => {
      expect(lineGross({ ...LINES.simple, quantity: 2.5 })).toBe(12.5);
    });
  });

  describe("lineDiscountAmt", () => {
    it("returns 0 when no discount", () => {
      expect(lineDiscountAmt(LINES.simple)).toBe(0);
    });

    it("calculates discount from percentage", () => {
      expect(lineDiscountAmt(LINES.withDiscount)).toBe(9);
    });

    it("handles 100% discount", () => {
      expect(lineDiscountAmt({ ...LINES.simple, discountPct: 100 })).toBe(50);
    });
  });

  describe("lineNet", () => {
    it("returns gross minus discount", () => {
      expect(lineNet(LINES.withDiscount)).toBe(81);
    });

    it("equals gross when no discount", () => {
      expect(lineNet(LINES.simple)).toBe(50);
    });
  });

  describe("calcLineTax — preview approximation", () => {
    it("approximates IVA 15% for vatCode 10", () => {
      const { vat } = calcLineTax(LINES.simple, TEST_VAT_RATES);
      expect(vat).toBeCloseTo(7.5, 2);
    });

    it("returns 0 for vatCode 0", () => {
      const { vat } = calcLineTax(LINES.vatZero, TEST_VAT_RATES);
      expect(vat).toBe(0);
    });

    it("approximates IVA 5% for vatCode 20", () => {
      const { vat } = calcLineTax(LINES.vat5pct, TEST_VAT_RATES);
      expect(vat).toBeCloseTo(4.5, 2);
    });

    it("returns ice=0 when no iceRates provided (degradación sin catálogo)", () => {
      const { ice } = calcLineTax(LINES.withIce, TEST_VAT_RATES);
      expect(ice).toBe(0);
    });

    it("returns ice=0 when iceCode not found in iceRates", () => {
      const { ice } = calcLineTax(LINES.withIce, TEST_VAT_RATES, { OTHER: 5 });
      expect(ice).toBe(0);
    });

    it("calcula ICE ad valorem cuando se provee el catálogo", () => {
      // net = 24 × 0.75 = 18; ICE 10% = 1.8
      const { ice } = calcLineTax(
        LINES.withIce,
        TEST_VAT_RATES,
        TEST_ICE_RATES,
      );
      expect(ice).toBeCloseTo(1.8, 2);
    });

    it("calcula IVA sobre base neta + ICE (normativa SRI)", () => {
      // net = 18, ice = 1.8, base IVA = 19.8, IVA 15% = 2.97
      const { vat, ice } = calcLineTax(
        LINES.withIce,
        TEST_VAT_RATES,
        TEST_ICE_RATES,
      );
      expect(ice).toBeCloseTo(1.8, 2);
      expect(vat).toBeCloseTo(2.97, 2);
    });

    it("línea sin iceCode: IVA calculado sobre base neta únicamente", () => {
      const { vat, ice } = calcLineTax(
        LINES.simple,
        TEST_VAT_RATES,
        TEST_ICE_RATES,
      );
      // net = 50, sin ice, base IVA = 50, IVA 15% = 7.5
      expect(ice).toBe(0);
      expect(vat).toBeCloseTo(7.5, 2);
    });
  });

  describe("calcSummary", () => {
    it("aggregates multiple lines correctly", () => {
      const all = [LINES.simple, LINES.withDiscount, LINES.vatZero];
      const s = calcSummary(all, TEST_VAT_RATES);
      expect(s.subtotal).toBe(50 + 90 + 200);
      expect(s.discount).toBe(0 + 9 + 0);
      expect(s.netSubtotal).toBe(s.subtotal - s.discount);
    });

    it("returns zeros for empty list", () => {
      const s = calcSummary([]);
      expect(s.subtotal).toBe(0);
      expect(s.discount).toBe(0);
      expect(s.total).toBe(0);
    });

    it("acumula ICE en total cuando se provee el catálogo", () => {
      // withIce: net=18, ice=1.8, base IVA=19.8, IVA=2.97
      const s = calcSummary([LINES.withIce], TEST_VAT_RATES, TEST_ICE_RATES);
      expect(s.ice).toBeCloseTo(1.8, 2);
      expect(s.vat).toBeCloseTo(2.97, 2);
      expect(s.total).toBeCloseTo(18 + 1.8 + 2.97, 2);
    });

    it("total sin ICE cuando no se provee catálogo (degradación)", () => {
      const s = calcSummary([LINES.withIce], TEST_VAT_RATES);
      expect(s.ice).toBe(0);
      // IVA sobre net (sin ice) = 18 × 15% = 2.7
      expect(s.vat).toBeCloseTo(2.7, 2);
    });
  });

  describe("mock backend data — structural integrity", () => {
    it("has consistent taxInclusiveTotal = taxableBase + vatAmount + iceAmount", () => {
      for (const d of mockBackendDetails) {
        expect(d.taxInclusiveTotal).toBeCloseTo(
          d.taxableBase + d.vatAmount + d.iceAmount,
          2,
        );
      }
    });

    it("has consistent taxableBase = (quantity * unitPrice) - discountAmount", () => {
      for (const d of mockBackendDetails) {
        expect(d.taxableBase).toBeCloseTo(
          d.quantity * d.unitPrice - d.discountAmount,
          2,
        );
      }
    });

    it("has sequential sortOrder", () => {
      mockBackendDetails.forEach((d, i) => {
        expect(d.sortOrder).toBe(i + 1);
      });
    });
  });
});
