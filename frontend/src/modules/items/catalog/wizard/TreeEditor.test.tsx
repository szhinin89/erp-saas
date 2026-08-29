// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { TreeEditorPage } from "./TreeEditor";
import { I18nProvider } from "../../../../i18n/i18n";
import {
  categoryNodeService,
  type CategoryNodeDto,
} from "../api/categoryNodeService";
import { message } from "../../../../lib/messages";

function renderPage() {
  return render(
    <I18nProvider>
      <TreeEditorPage />
    </I18nProvider>,
  );
}

/**
 * CRITICAL-CONFIRMATIONS-CLEANUP-07 — residuo encontrado en el barrido: activar/desactivar una
 * categoría no pedía confirmación previa ni mostraba message.success. Se agrega confirmación
 * (message.confirm) y feedback real (message.success/error), sin cambiar el árbol ni el payload
 * de categoryNodeService.disable/enable.
 */

vi.mock("../api/categoryNodeService", () => ({
  categoryNodeService: {
    getTree: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    disable: vi.fn(),
    enable: vi.fn(),
  },
}));

vi.mock("../../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const ROOT_NODE: CategoryNodeDto = {
  id: "node-1",
  parentId: null,
  code: "FAM01",
  name: "Familia Uno",
  description: null,
  level: "Family",
  path: "FAM01",
  depth: 0,
  sortOrder: 1,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: null,
};

afterEach(() => cleanup());

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(categoryNodeService.getTree).mockResolvedValue({
    nodes: [ROOT_NODE],
    maxDepth: 3,
  });
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("TreeEditorPage — activar/desactivar categoría: confirmación y feedback", () => {
  it("pide confirmación antes de desactivar", async () => {
    vi.mocked(categoryNodeService.disable).mockResolvedValue(true);
    renderPage();
    await waitFor(() => expect(screen.getByText("Familia Uno")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Desactivar"));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(categoryNodeService.disable).toHaveBeenCalledWith("node-1");
    });
  });

  it("si se cancela, no llama a categoryNodeService.disable", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    renderPage();
    await waitFor(() => expect(screen.getByText("Familia Uno")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Desactivar"));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(categoryNodeService.disable).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente muestra message.success", async () => {
    vi.mocked(categoryNodeService.disable).mockResolvedValue(true);
    renderPage();
    await waitFor(() => expect(screen.getByText("Familia Uno")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Desactivar"));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Categoría desactivada correctamente."),
    );
  });

  it("si falla, muestra el mensaje real del backend y no éxito", async () => {
    vi.mocked(categoryNodeService.disable).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "La categoría tiene ítems asociados." } },
      },
    });
    renderPage();
    await waitFor(() => expect(screen.getByText("Familia Uno")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Desactivar"));

    await waitFor(() =>
      expect(message.error).toHaveBeenCalledWith("La categoría tiene ítems asociados."),
    );
    expect(message.success).not.toHaveBeenCalled();
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(categoryNodeService.disable).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Familia Uno")).toBeTruthy());
    fireEvent.click(screen.getByTitle("Desactivar"));
    await waitFor(() => expect(categoryNodeService.disable).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
