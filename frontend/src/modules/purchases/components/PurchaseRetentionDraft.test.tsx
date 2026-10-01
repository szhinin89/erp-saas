// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import type { PurchaseInvoiceDto, RetentionPreviewDto } from "../api/purchaseService";
import { PurchaseRetentionDraft, type PurchaseRetentionDraftContext } from "./PurchaseRetentionDraft";

/**
 * ZH-PURCHASE-RETENTION-CONFIRM-01 — definición de la retención en el borrador de la compra: vista
 * previa precargada (no editable), decisión de emitirla al confirmar, punto de emisión y fecha. No
 * existe una acción "Calcular"/"Emitir" separada.
 */

afterEach(() => cleanup());

const PREVIEW: RetentionPreviewDto = {
  lines: [
    {
      taxType: "IVA",
      retentionCode: "725",
      retentionCodeName: "Retención IVA 30%",
      taxableBase: 15,
      retentionPct: 30,
      amountRetained: 4.5,
    },
  ],
  totalRetainedVat: 4.5,
  totalRetainedIncome: 0,
  totalRetainedIsd: 0,
  totalRetained: 4.5,
  skipReason: null,
};

function ctxWith(overrides: Partial<PurchaseRetentionDraftContext> = {}): PurchaseRetentionDraftContext {
  return {
    editing: { id: "purchase-1", status: "Draft" } as PurchaseInvoiceDto,
    saving: false,
    whPreview: PREVIEW,
    whPreviewError: null,
    whLoading: false,
    refreshRetentionPreview: vi.fn().mockResolvedValue(undefined),
    retentionIntent: { appliesRetention: false, emissionPointId: "", issueDate: "2026-09-30" },
    updateRetentionIntent: vi.fn(),
    canApplyRetention: true,
    emissionPoints: [
      {
        id: "ep-1",
        establishmentId: "est-1",
        establishmentCode: "001",
        establishmentName: "Matriz",
        branchName: null,
        code: "001",
        name: "Principal",
        emissionType: "Electronic",
        isDefault: true,
        isActive: true,
        createdAt: "2026-01-01T00:00:00Z",
      },
    ] as PurchaseRetentionDraftContext["emissionPoints"],
    canReadEmissionPoints: true,
    ...overrides,
  };
}

function renderDraft(ctx: PurchaseRetentionDraftContext) {
  return render(
    <I18nProvider>
      <PurchaseRetentionDraft ctx={ctx} />
    </I18nProvider>,
  );
}

describe("PurchaseRetentionDraft", () => {
  it("muestra la retención propuesta precargada y la opción de emitirla al confirmar, sin acciones Calcular/Emitir", () => {
    renderDraft(ctxWith());

    expect(screen.getByText("725")).toBeTruthy();
    expect(screen.getByText("Retención IVA 30%")).toBeTruthy();
    expect(screen.getByRole("button", { name: /Emitir la retención al confirmar la compra/ })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /^Calcular$/ })).toBeNull();
    expect(screen.queryByRole("button", { name: /^Emitir$/ })).toBeNull();
    expect(screen.queryByText("Punto de emisión")).toBeNull();
  });

  it("activar la opción actualiza la intención", () => {
    const ctx = ctxWith();
    renderDraft(ctx);

    fireEvent.click(screen.getByRole("button", { name: /Emitir la retención al confirmar la compra/ }));

    expect(ctx.updateRetentionIntent).toHaveBeenCalledWith({ appliesRetention: true });
  });

  it("con la intención activa pide punto de emisión y fecha, y avisa que el número se genera al confirmar", () => {
    const ctx = ctxWith({
      retentionIntent: { appliesRetention: true, emissionPointId: "ep-1", issueDate: "2026-09-30" },
    });
    renderDraft(ctx);

    expect(screen.getByRole("button", { name: /Emitir la retención al confirmar la compra/ }).getAttribute("aria-pressed")).toBe("true");
    expect(screen.getByText("El número de retención se generará automáticamente al confirmar la compra.")).toBeTruthy();
    const select = screen.getByRole("combobox") as HTMLSelectElement;
    expect(select.value).toBe("ep-1");
    fireEvent.change(select, { target: { value: "" } });
    expect(ctx.updateRetentionIntent).toHaveBeenCalledWith({ emissionPointId: "" });
  });

  it("sin permiso para leer puntos de emisión lo indica y deshabilita la selección", () => {
    renderDraft(
      ctxWith({
        canReadEmissionPoints: false,
        retentionIntent: { appliesRetention: true, emissionPointId: "", issueDate: "2026-09-30" },
      }),
    );

    expect(screen.getByText("Sin permiso para leer puntos de emisión.")).toBeTruthy();
    expect((screen.getByRole("combobox") as HTMLSelectElement).disabled).toBe(true);
  });

  it("compra no elegible: explica el motivo y no ofrece emitir retención", () => {
    renderDraft(
      ctxWith({
        canApplyRetention: false,
        whPreview: { ...PREVIEW, lines: [], totalRetained: 0, totalRetainedVat: 0, skipReason: "La empresa no está configurada como agente de retención de IVA." },
      }),
    );

    expect(screen.getByText("Esta compra no genera retención con la configuración actual.")).toBeTruthy();
    expect(screen.getByText("La empresa no está configurada como agente de retención de IVA.")).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Emitir la retención al confirmar la compra/ })).toBeNull();
  });

  it("muestra el estado de carga de la vista previa", () => {
    renderDraft(ctxWith({ whLoading: true, whPreview: null }));

    expect(screen.getByText("Calculando la retención propuesta...")).toBeTruthy();
  });

  it("si la vista previa falla muestra el error y permite reintentar", () => {
    const ctx = ctxWith({ whPreview: null, whPreviewError: "Fallo de elegibilidad" });
    renderDraft(ctx);

    expect(screen.getByText("Fallo de elegibilidad")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Reintentar" }));
    expect(ctx.refreshRetentionPreview).toHaveBeenCalledWith("purchase-1");
  });
});
