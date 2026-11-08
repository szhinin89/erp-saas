// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup } from "@testing-library/react";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { ExpenseCategoriesPage } from "./ExpenseCategoriesPage";
import { expenseCategoryService } from "../api/expenseCategoryService";
import { accountLookupFacade } from "../../accounting/facades/accountLookupFacade";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-PURCHASES-EXPENSES-03 — cubre "Activar/desactivar categoría de gastos":
 * confirma antes de ejecutar (mensaje distinto por dirección), no llama al backend si se cancela,
 * éxito muestra message.success, fallo muestra el mensaje real vía formatApiRequestError.
 */

vi.mock("../api/expenseCategoryService", () => ({
  expenseCategoryService: {
    getTree: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    activate: vi.fn(),
    deactivate: vi.fn(),
  },
}));

vi.mock("../../accounting/facades/accountLookupFacade", () => ({
  accountLookupFacade: {
    listAccounts: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const ACTIVE_NODE = {
  id: "node-1",
  companyId: "company-1",
  parentId: null,
  level: "Type" as const,
  code: "T01",
  name: "Servicios básicos",
  description: null,
  accountingAccountId: null,
  isActive: true,
  children: [],
};

const INACTIVE_NODE = {
  ...ACTIVE_NODE,
  id: "node-2",
  code: "T02",
  name: "Viáticos",
  isActive: false,
};

function renderPage() {
  return render(<ExpenseCategoriesPage />);
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
  vi.mocked(accountLookupFacade.listAccounts).mockResolvedValue([]);
  vi.mocked(expenseCategoryService.getTree).mockResolvedValue([
    ACTIVE_NODE,
    INACTIVE_NODE,
  ]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(() => {
  cleanup();
});

describe("ExpenseCategoriesPage — activar/desactivar: confirmación y feedback", () => {
  it("desactivar pide confirmación explicando que no elimina el histórico, y llama a deactivate al confirmar", async () => {
    vi.mocked(expenseCategoryService.deactivate).mockResolvedValue({ ...ACTIVE_NODE });

    renderPage();
    await waitFor(() => expect(screen.getByText("Servicios básicos")).toBeTruthy());

    fireEvent.click(screen.getByLabelText("Desactivar Servicios básicos"));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(expenseCategoryService.deactivate).toHaveBeenCalledWith("node-1");
    });

    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/dejará de estar disponible/i);
    expect(String(options.message)).toMatch(/no se modifican|no.*elimina|históric/i);
    expect(message.success).toHaveBeenCalledWith("Nodo desactivado correctamente.");
  });

  it("activar pide confirmación explicando que vuelve a estar disponible, y llama a activate al confirmar", async () => {
    vi.mocked(expenseCategoryService.activate).mockResolvedValue({ ...ACTIVE_NODE });

    renderPage();
    await waitFor(() => expect(screen.getByText("Viáticos")).toBeTruthy());

    fireEvent.click(screen.getByLabelText("Activar Viáticos"));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(expenseCategoryService.activate).toHaveBeenCalledWith("node-2");
    });

    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/volverá a estar disponible/i);
    expect(message.success).toHaveBeenCalledWith("Nodo activado correctamente.");
  });

  it("si se cancela la confirmación, no llama a deactivate/activate", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);

    renderPage();
    await waitFor(() => expect(screen.getByText("Servicios básicos")).toBeTruthy());

    fireEvent.click(screen.getByLabelText("Desactivar Servicios básicos"));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(expenseCategoryService.deactivate).not.toHaveBeenCalled();
    expect(expenseCategoryService.activate).not.toHaveBeenCalled();
  });

  it("si el backend falla, muestra el mensaje real y no llama message.success", async () => {
    vi.mocked(expenseCategoryService.deactivate).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El nodo tiene gastos registrados este mes." } },
      },
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Servicios básicos")).toBeTruthy());

    fireEvent.click(screen.getByLabelText("Desactivar Servicios básicos"));

    await waitFor(() => {
      expect(message.error).toHaveBeenCalledWith(
        "El nodo tiene gastos registrados este mes.",
      );
    });
    expect(message.success).not.toHaveBeenCalled();
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(expenseCategoryService.deactivate).mockResolvedValue({ ...ACTIVE_NODE });

    renderPage();
    await waitFor(() => expect(screen.getByText("Servicios básicos")).toBeTruthy());
    fireEvent.click(screen.getByLabelText("Desactivar Servicios básicos"));
    await waitFor(() => expect(expenseCategoryService.deactivate).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});

// UIUX-EXPENSE-CATEGORIES-MODAL — creación/edición en ZHModal, árbol compacto expandible,
// protección de cambios sin guardar, foco y conservación de la selección.

const ACCOUNT = {
  id: "acc-1",
  code: "5.1.01",
  name: "Energía eléctrica",
  isActive: true,
  allowsPosting: true,
  accountType: "Expense",
};

const SUBCATEGORY = {
  id: "sub-1",
  companyId: "company-1",
  parentId: "cat-1",
  level: "Subcategory" as const,
  code: "S01",
  name: "Luz eléctrica",
  description: "Consumo mensual del local",
  accountingAccountId: "acc-1",
  isActive: true,
  children: [],
};

const CATEGORY = {
  id: "cat-1",
  companyId: "company-1",
  parentId: "type-1",
  level: "Category" as const,
  code: "C01",
  name: "Servicios",
  description: null,
  accountingAccountId: null,
  isActive: true,
  children: [SUBCATEGORY],
};

const TYPE = {
  id: "type-1",
  companyId: "company-1",
  parentId: null,
  level: "Type" as const,
  code: "T01",
  name: "Operativos",
  description: null,
  accountingAccountId: null,
  isActive: true,
  children: [CATEGORY],
};

function dialog() {
  return screen.queryByRole("dialog");
}

async function renderHierarchy() {
  vi.mocked(expenseCategoryService.getTree).mockResolvedValue([TYPE]);
  vi.mocked(accountLookupFacade.listAccounts).mockResolvedValue([
    ACCOUNT,
  ] as never);
  renderPage();
  await waitFor(() => expect(screen.getByText("Luz eléctrica")).toBeTruthy());
}

function typeInto(label: RegExp, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

function nodeButton(name: string) {
  return screen.getByText(name).closest("button");
}

describe("ExpenseCategoriesPage — editor en ZHModal", () => {
  it("crear tipo: modal con título, nivel raíz, sin cuenta; guarda, cierra y recarga", async () => {
    vi.mocked(expenseCategoryService.create).mockResolvedValue({ ...TYPE, id: "type-2" });
    await renderHierarchy();

    fireEvent.click(screen.getByRole("button", { name: /Crear tipo/ }));

    expect(dialog()?.getAttribute("aria-label")).toBe("Crear tipo de gasto");
    expect(screen.getByText("Nivel raíz del catálogo de gastos")).toBeTruthy();
    expect(screen.queryByLabelText(/Cuenta contable destino/)).toBeNull();

    typeInto(/Código/, "T02");
    typeInto(/Nombre/, "Administrativos");
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() =>
      expect(expenseCategoryService.create).toHaveBeenCalledWith({
        code: "T02",
        name: "Administrativos",
        description: null,
        accountingAccountId: null,
        level: "Type",
        parentId: null,
      }),
    );
    await waitFor(() => expect(dialog()).toBeNull());
    expect(message.success).toHaveBeenCalledWith("Nodo del catálogo creado correctamente.");
    await waitFor(() => expect(expenseCategoryService.getTree).toHaveBeenCalledTimes(2));
  });

  it("crear categoría: muestra la ruta del tipo padre", async () => {
    await renderHierarchy();
    fireEvent.click(screen.getByLabelText("Crear categoría dentro de Operativos"));

    expect(dialog()?.getAttribute("aria-label")).toBe("Crear categoría");
    expect(screen.getByText("Dentro de: T01 · Operativos")).toBeTruthy();
    expect(screen.queryByLabelText(/Cuenta contable destino/)).toBeNull();
  });

  it("crear subcategoría: ruta completa y cuenta obligatoria (validación cliente)", async () => {
    vi.mocked(expenseCategoryService.create).mockResolvedValue({ ...SUBCATEGORY, id: "sub-2" });
    await renderHierarchy();
    fireEvent.click(screen.getByLabelText("Crear subcategoría dentro de Servicios"));

    expect(dialog()?.getAttribute("aria-label")).toBe("Crear subcategoría");
    expect(screen.getByText("Dentro de: T01 · Operativos › C01 · Servicios")).toBeTruthy();

    typeInto(/Código/, "S02");
    typeInto(/Nombre/, "Agua potable");
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() =>
      expect(screen.getByText("La subcategoría requiere una cuenta contable.")).toBeTruthy(),
    );
    expect(expenseCategoryService.create).not.toHaveBeenCalled();
    expect(dialog()).toBeTruthy();

    typeInto(/Cuenta contable destino/, "acc-1");
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));
    await waitFor(() =>
      expect(expenseCategoryService.create).toHaveBeenCalledWith(
        expect.objectContaining({
          level: "Subcategory",
          parentId: "cat-1",
          accountingAccountId: "acc-1",
        }),
      ),
    );
    await waitFor(() => expect(dialog()).toBeNull());
  });

  it("editar: datos cargados, ubicación del padre, update con el id y selección conservada", async () => {
    vi.mocked(expenseCategoryService.update).mockResolvedValue({ ...SUBCATEGORY });
    await renderHierarchy();
    fireEvent.click(screen.getByLabelText("Editar Luz eléctrica"));

    expect(dialog()?.getAttribute("aria-label")).toBe("Editar subcategoría");
    expect(screen.getByText("Ubicación: T01 · Operativos › C01 · Servicios")).toBeTruthy();
    expect((screen.getByLabelText(/Código/) as HTMLInputElement).value).toBe("S01");
    expect((screen.getByLabelText(/Nombre/) as HTMLInputElement).value).toBe("Luz eléctrica");
    expect((screen.getByLabelText(/Cuenta contable destino/) as HTMLSelectElement).value).toBe("acc-1");

    typeInto(/Nombre/, "Luz");
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));
    await waitFor(() =>
      expect(expenseCategoryService.update).toHaveBeenCalledWith("sub-1", {
        code: "S01",
        name: "Luz",
        description: "Consumo mensual del local",
        accountingAccountId: "acc-1",
      }),
    );
    await waitFor(() => expect(dialog()).toBeNull());
    await waitFor(() => expect(nodeButton("Luz eléctrica")?.getAttribute("aria-current")).toBe("true"));
  });

  it("error de API: el modal sigue abierto con el error en el campo", async () => {
    vi.mocked(expenseCategoryService.create).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 422,
        data: { data: { errors: { Code: ["El código ya existe."] } } },
      },
    });
    await renderHierarchy();
    fireEvent.click(screen.getByRole("button", { name: /Crear tipo/ }));
    typeInto(/Código/, "T01");
    typeInto(/Nombre/, "Duplicado");
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() => expect(screen.getByText("El código ya existe.")).toBeTruthy());
    expect(message.error).toHaveBeenCalled();
    expect(message.success).not.toHaveBeenCalled();
    expect(dialog()).toBeTruthy();
  });

  it("cancelar y Escape sin cambios cierran sin confirmar ni llamar a la API", async () => {
    await renderHierarchy();
    fireEvent.click(screen.getByLabelText("Editar Operativos"));
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(dialog()).toBeNull());

    fireEvent.click(screen.getByLabelText("Editar Operativos"));
    fireEvent.keyDown(window, { key: "Escape" });
    await waitFor(() => expect(dialog()).toBeNull());

    expect(message.confirm).not.toHaveBeenCalled();
    expect(expenseCategoryService.update).not.toHaveBeenCalled();
  });

  it("con cambios sin guardar pide confirmación oficial; si no se descarta, sigue abierto", async () => {
    vi.mocked(message.confirm).mockResolvedValueOnce(false).mockResolvedValueOnce(true);
    await renderHierarchy();
    fireEvent.click(screen.getByLabelText("Editar Operativos"));
    typeInto(/Nombre/, "Operativos y logística");

    fireEvent.keyDown(window, { key: "Escape" });
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    expect(vi.mocked(message.confirm).mock.calls[0][0].title).toBe("Descartar cambios");
    expect(dialog()).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(dialog()).toBeNull());
    expect(message.confirm).toHaveBeenCalledTimes(2);
    expect(expenseCategoryService.update).not.toHaveBeenCalled();
  });

  it("durante el guardado no se puede cerrar (Escape/Cancelar)", async () => {
    let resolveCreate: (value: unknown) => void = () => {};
    vi.mocked(expenseCategoryService.create).mockReturnValue(
      new Promise((resolve) => {
        resolveCreate = resolve;
      }) as never,
    );
    await renderHierarchy();
    fireEvent.click(screen.getByRole("button", { name: /Crear tipo/ }));
    typeInto(/Código/, "T02");
    typeInto(/Nombre/, "Administrativos");
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() => expect(screen.getByRole("button", { name: "Guardando..." })).toBeTruthy());
    expect((screen.getByRole("button", { name: "Cancelar" }) as HTMLButtonElement).disabled).toBe(true);
    fireEvent.keyDown(window, { key: "Escape" });
    expect(dialog()).toBeTruthy();
    expect(message.confirm).not.toHaveBeenCalled();

    resolveCreate({ ...TYPE, id: "type-2" });
    await waitFor(() => expect(dialog()).toBeNull());
  });

  it("foco: al abrir va al primer campo y al cerrar vuelve al control que abrió el modal", async () => {
    await renderHierarchy();
    const opener = screen.getByLabelText("Editar Servicios");
    opener.focus();
    fireEvent.click(opener);

    expect(document.activeElement).toBe(screen.getByLabelText(/Código/));
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(document.activeElement).toBe(screen.getByLabelText("Editar Servicios")));
  });
});

describe("ExpenseCategoriesPage — permisos", () => {
  it("solo view: no muestra crear, editar ni activar/desactivar", async () => {
    vi.mocked(usePermissionsUi).mockReturnValue({
      canShow: () => true,
      has: (perm: string) => perm === "expenses.catalog.view",
      isAdminRole: false,
    });
    await renderHierarchy();

    expect(screen.queryByRole("button", { name: /Crear tipo/ })).toBeNull();
    expect(screen.queryByLabelText("Crear categoría dentro de Operativos")).toBeNull();
    expect(screen.queryByLabelText("Editar Luz eléctrica")).toBeNull();
    expect(screen.queryByLabelText("Desactivar Luz eléctrica")).toBeNull();
  });

  it("con create pero sin update: crea pero no edita", async () => {
    vi.mocked(usePermissionsUi).mockReturnValue({
      canShow: () => true,
      has: (perm: string) => perm !== "expenses.catalog.update",
      isAdminRole: false,
    });
    await renderHierarchy();

    expect(screen.getByRole("button", { name: /Crear tipo/ })).toBeTruthy();
    expect(screen.queryByLabelText("Editar Operativos")).toBeNull();
  });
});

describe("ExpenseCategoriesPage — árbol expandible", () => {
  it("contraer/expandir un tipo con disclosure accesible, sin seleccionar ni editar", async () => {
    await renderHierarchy();
    const toggle = screen.getByLabelText("Contraer Operativos (1)");
    expect(toggle.getAttribute("aria-expanded")).toBe("true");
    const panel = document.getElementById(toggle.getAttribute("aria-controls") ?? "");
    expect(panel?.hidden).toBe(false);

    fireEvent.click(toggle);
    const collapsed = screen.getByLabelText("Expandir Operativos (1)");
    expect(collapsed.getAttribute("aria-expanded")).toBe("false");
    expect(panel?.hidden).toBe(true);
    expect(dialog()).toBeNull();
    expect(nodeButton("Operativos")?.getAttribute("aria-current")).toBeNull();

    fireEvent.click(collapsed);
    expect(panel?.hidden).toBe(false);
  });

  it("contraer todo conserva visible la ruta del nodo seleccionado; expandir todo la restablece", async () => {
    await renderHierarchy();
    fireEvent.click(screen.getByText("Luz eléctrica"));
    expect(nodeButton("Luz eléctrica")?.getAttribute("aria-current")).toBe("true");

    fireEvent.click(screen.getByRole("button", { name: /Contraer todo/ }));
    expect(screen.getByLabelText("Contraer Operativos (1)").getAttribute("aria-expanded")).toBe("true");
    expect(screen.getByLabelText("Contraer Servicios (1)").getAttribute("aria-expanded")).toBe("true");

    fireEvent.click(screen.getByText("Operativos"));
    fireEvent.click(screen.getByRole("button", { name: /Contraer todo/ }));
    expect(screen.getByLabelText("Expandir Operativos (1)").getAttribute("aria-expanded")).toBe("false");

    fireEvent.click(screen.getByRole("button", { name: /Expandir todo/ }));
    expect(screen.getByLabelText("Contraer Operativos (1)").getAttribute("aria-expanded")).toBe("true");
    expect(screen.getByLabelText("Contraer Servicios (1)").getAttribute("aria-expanded")).toBe("true");
  });

  it("muestra la descripción solo cuando existe", async () => {
    await renderHierarchy();
    expect(screen.getByText("Consumo mensual del local")).toBeTruthy();
    expect(document.querySelectorAll(".exp-cat-node__description")).toHaveLength(1);
  });
});
