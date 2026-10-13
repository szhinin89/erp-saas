// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseReceptionBatchActions } from "./PurchaseReceptionBatchActions";

afterEach(() => cleanup());

function show(overrides: Partial<Parameters<typeof PurchaseReceptionBatchActions>[0]> = {}) {
  const onRefreshStatuses = vi.fn();
  const onDownloadPendingXml = vi.fn();
  const view = render(
    <I18nProvider>
      <PurchaseReceptionBatchActions
        importedCount={37}
        uploading={false}
        downloadingXml={false}
        refreshing={false}
        onRefreshStatuses={onRefreshStatuses}
        onDownloadPendingXml={onDownloadPendingXml}
        {...overrides}
      />
    </I18nProvider>,
  );
  return { ...view, onRefreshStatuses, onDownloadPendingXml };
}

const refreshButton = () => screen.getByRole("button", { name: /Actualizar estados|Actualizando/ }) as HTMLButtonElement;
const downloadButton = () => screen.getByRole("button", { name: /Descargar XML pendientes/ }) as HTMLButtonElement;

describe("FASE 5 PURCHASE-UX — barra compacta de acciones del lote", () => {
  it("sin importación no se muestra", () => {
    const { container } = show({ importedCount: null });
    expect(container.querySelector(".pur-reception-batch")).toBeNull();
  });

  it("muestra etiqueta + contador y ambos botones habilitados en reposo", () => {
    const { onRefreshStatuses, onDownloadPendingXml } = show();
    expect(screen.getByText("Acciones del lote")).toBeTruthy();
    expect(screen.getByText(/37 comprobantes/)).toBeTruthy();
    fireEvent.click(refreshButton());
    fireEvent.click(downloadButton());
    expect(onRefreshStatuses).toHaveBeenCalledTimes(1);
    expect(onDownloadPendingXml).toHaveBeenCalledTimes(1);
  });

  it.each([
    ["actualizando estados", { refreshing: true }],
    ["descargando XML", { downloadingXml: true }],
    ["importando TXT", { uploading: true }],
  ])("%s: ambos botones deshabilitados", (_, state) => {
    show(state);
    expect(refreshButton().disabled).toBe(true);
    expect(downloadButton().disabled).toBe(true);
  });

  it("mientras refresca el botón dice 'Actualizando…'", () => {
    show({ refreshing: true });
    expect(refreshButton().textContent).toContain("Actualizando…");
  });

  it("el progreso solo ocupa espacio cuando existe", () => {
    const { container, rerender } = show();
    const section = container.querySelector(".pur-reception-batch")!;
    expect(section.children).toHaveLength(1); // solo la fila: nada reservado debajo

    rerender(
      <I18nProvider>
        <PurchaseReceptionBatchActions
          importedCount={37}
          uploading={false}
          downloadingXml
          refreshing={false}
          onRefreshStatuses={vi.fn()}
          onDownloadPendingXml={vi.fn()}
          progress={<div data-testid="progress" />}
        />
      </I18nProvider>,
    );
    expect(container.querySelector(".pur-reception-batch")!.children).toHaveLength(2);
    expect(screen.getByTestId("progress")).toBeTruthy();
  });
});
