// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { I18nProvider } from "../i18n/i18n";
import { BranchSelectorModal } from "./BranchSelectorModal";

const branchOptions = [
  { id: "branch-1", name: "Matriz", isMainBranch: true },
  { id: "branch-2", name: "Norte", isMainBranch: false },
];

afterEach(() => {
  cleanup();
});

function renderModal(
  props: Partial<Parameters<typeof BranchSelectorModal>[0]> = {},
) {
  const onSelect = props.onSelect ?? vi.fn();
  const onRetry = props.onRetry ?? vi.fn();
  render(
    <I18nProvider>
      <BranchSelectorModal
        open={props.open ?? true}
        loading={props.loading ?? false}
        contextLoading={props.contextLoading ?? false}
        options={props.options ?? branchOptions}
        error={props.error ?? ""}
        switching={props.switching ?? false}
        onSelect={onSelect}
        onRetry={onRetry}
      />
    </I18nProvider>,
  );
  return { onSelect, onRetry };
}

describe("BranchSelectorModal", () => {
  it("no renderiza contenido cuando open=false", () => {
    renderModal({ open: false });
    expect(screen.queryByText("Matriz")).toBeNull();
  });

  it("muestra las sucursales disponibles y permite seleccionar una", () => {
    const { onSelect } = renderModal();

    expect(screen.getAllByText("Matriz").length).toBeGreaterThan(0);
    expect(screen.getByText("Norte")).toBeTruthy();

    const buttons = screen.getAllByRole("button", { name: "Ingresar" });
    fireEvent.click(buttons[0]);

    expect(onSelect).toHaveBeenCalledWith("branch-1");
  });

  it("muestra el estado de carga en vez de la lista", () => {
    renderModal({ loading: true });

    expect(screen.getByText("Cargando sucursales disponibles…")).toBeTruthy();
    expect(screen.queryByText("Matriz")).toBeNull();
  });

  it("muestra el mensaje de contexto cargando en vez del de sucursales cuando session/context aún resuelve", () => {
    renderModal({ loading: true, contextLoading: true });

    expect(screen.getByText("Cargando contexto operativo…")).toBeTruthy();
    expect(
      screen.queryByText("Cargando sucursales disponibles…"),
    ).toBeNull();
  });

  it("no muestra 'sin sucursales asignadas' mientras loading es true", () => {
    renderModal({ loading: true, options: [] });

    expect(
      screen.queryByText(
        "No tiene sucursales asignadas. Contacte a un administrador.",
      ),
    ).toBeNull();
  });

  it("muestra 'sin sucursales asignadas' solo cuando terminó de cargar y la lista real está vacía", () => {
    renderModal({ loading: false, options: [] });

    expect(
      screen.getByText(
        "No tiene sucursales asignadas. Contacte a un administrador.",
      ),
    ).toBeTruthy();
  });

  it("muestra el mensaje de error y permite reintentar", () => {
    const { onRetry } = renderModal({
      error: "No se pudo cambiar de sucursal.",
    });

    expect(screen.getByText("No se pudo cambiar de sucursal.")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Reintentar" }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});
