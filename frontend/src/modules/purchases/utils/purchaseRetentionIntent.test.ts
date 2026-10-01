import { describe, expect, it } from "vitest";
import type { RetentionPreviewDto } from "../api/purchaseService";
import {
  buildPurchaseRetentionIntent,
  canApplyPurchaseRetention,
  emptyPurchaseRetentionIntent,
  isPurchaseRetentionIntentComplete,
  purchaseRetentionLines,
} from "./purchaseRetentionIntent";

const PREVIEW: RetentionPreviewDto = {
  lines: [
    { taxType: "IVA", retentionCode: "725", retentionCodeName: "Ret. IVA 30%", taxableBase: 15, retentionPct: 30, amountRetained: 4.5 },
    { taxType: "RENTA", retentionCode: "303", retentionCodeName: "Honorarios", taxableBase: 100, retentionPct: 10, amountRetained: 10 },
    { taxType: "RENTA", retentionCode: "332", retentionCodeName: "Sin monto", taxableBase: 100, retentionPct: 0, amountRetained: 0 },
  ],
  totalRetainedVat: 4.5,
  totalRetainedIncome: 10,
  totalRetainedIsd: 0,
  totalRetained: 14.5,
  skipReason: null,
};

describe("purchaseRetentionIntent (ZH-PURCHASE-RETENTION-CONFIRM-01)", () => {
  it("precarga las líneas de la vista previa con el contrato de emisión (IVA/RENTA → Vat/Income), sin las que no retienen", () => {
    expect(purchaseRetentionLines(PREVIEW)).toEqual([
      { taxType: "Vat", retentionCode: "725", baseAmount: 15, retentionRate: 30, retainedAmount: 4.5, retentionCodeDescription: "Ret. IVA 30%" },
      { taxType: "Income", retentionCode: "303", baseAmount: 100, retentionRate: 10, retainedAmount: 10, retentionCodeDescription: "Honorarios" },
    ]);
  });

  it("solo se puede aplicar si la vista previa propone líneas", () => {
    expect(canApplyPurchaseRetention(PREVIEW)).toBe(true);
    expect(canApplyPurchaseRetention(null)).toBe(false);
    expect(canApplyPurchaseRetention({ ...PREVIEW, lines: [] })).toBe(false);
  });

  it("sin intención no se envía nada (confirmación de siempre)", () => {
    const state = emptyPurchaseRetentionIntent("2026-09-30");

    expect(isPurchaseRetentionIntentComplete(PREVIEW, state)).toBe(true);
    expect(buildPurchaseRetentionIntent(PREVIEW, state)).toBeUndefined();
  });

  it("con intención exige punto de emisión, fecha y líneas", () => {
    const base = { appliesRetention: true, emissionPointId: "ep-1", issueDate: "2026-09-30" };

    expect(isPurchaseRetentionIntentComplete(PREVIEW, base)).toBe(true);
    expect(isPurchaseRetentionIntentComplete(PREVIEW, { ...base, emissionPointId: "" })).toBe(false);
    expect(isPurchaseRetentionIntentComplete(PREVIEW, { ...base, issueDate: "" })).toBe(false);
    expect(isPurchaseRetentionIntentComplete({ ...PREVIEW, lines: [] }, base)).toBe(false);
    expect(buildPurchaseRetentionIntent(PREVIEW, { ...base, emissionPointId: "" })).toBeUndefined();
  });

  it("construye la intención completa, nunca con número de retención", () => {
    const intent = buildPurchaseRetentionIntent(PREVIEW, {
      appliesRetention: true,
      emissionPointId: "ep-1",
      issueDate: "2026-09-30",
    });

    expect(intent).toEqual({
      appliesRetention: true,
      emissionPointId: "ep-1",
      issueDate: "2026-09-30",
      lines: purchaseRetentionLines(PREVIEW),
    });
    expect(intent).not.toHaveProperty("retentionNumber");
  });
});
