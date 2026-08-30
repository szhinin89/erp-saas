// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ChartOfAccountsPage } from "./ChartOfAccountsPage";
import { accountingApi } from "../api/accountingApi";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-INVENTORY-ACCOUNTING-05 — "Activar/desactivar cuenta contable":
 * confirma antes de ejecutar, no llama al backend si se cancela, éxito muestra message.success,
 * fallo muestra el mensaje real vía formatApiRequestError. No cambia validaciones/lógica contable.
 */

vi.mock("../api/accountingApi", () => ({
  accountingApi: {
    listAccounts: vi.fn(),
    createAccount: vi.fn(),
    updateAccount: vi.fn(),
    disableAccount: vi.fn(),
    enableAccount: vi.fn(),
  },
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const ACTIVE_ACCOUNT = {
  id: "acc-1",
  code: "1.1.01",
  name: "Caja general",
  parentAccountId: null,
  parentAccountCode: null,
  parentAccountName: null,
  level: 0,
  accountType: "Asset",
  nature: "Debit",
  allowsPosting: true,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: null,
};

function renderPage() {
  return render(
    <I18nProvider>
      <ChartOfAccountsPage />
    </I18nProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(accountingApi.listAccounts).mockResolvedValue([ACTIVE_ACCOUNT]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(() => cleanup());

describe("ChartOfAccountsPage — activar/desactivar cuenta: confirmación y feedback", () => {
  it("ordena por código contable y renderiza nombres limpios con guías visuales por código", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue([
      {
        ...ACTIVE_ACCOUNT,
        id: "acc-5",
        code: "1.1.01.002",
        name: "L Caja chica",
        level: 1,
      },
      {
        ...ACTIVE_ACCOUNT,
        id: "acc-3",
        code: "1.1.01",
        name: "Efectivo y equivalentes",
        level: 0,
        allowsPosting: false,
      },
      {
        ...ACTIVE_ACCOUNT,
        id: "acc-1",
        code: "1",
        name: "Activo",
        level: 0,
        allowsPosting: false,
      },
      {
        ...ACTIVE_ACCOUNT,
        id: "acc-2",
        code: "1.1",
        name: "Activo corriente",
        level: 0,
        allowsPosting: false,
      },
      {
        ...ACTIVE_ACCOUNT,
        id: "acc-4",
        code: "1.1.01.001",
        name: "Caja General",
        level: 1,
      },
      {
        ...ACTIVE_ACCOUNT,
        id: "acc-6",
        code: "1.1.02",
        name: "│ ├ Bancos",
        level: 0,
        allowsPosting: false,
      },
    ]);

    const { container } = renderPage();
    await waitFor(() => expect(screen.getByText("Caja chica")).toBeTruthy());

    const compactFilterBar = container.querySelector(".coa-list-filters");
    expect(compactFilterBar).toBeTruthy();
    expect(screen.getByPlaceholderText("Buscar por código o nombre...")).toBeTruthy();
    expect(screen.getByText("Todos los tipos")).toBeTruthy();
    expect(screen.getByText("Todos los estados")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Actualizar" })).toBeTruthy();

    const rows = Array.from(container.querySelectorAll("tbody tr"));
    expect(rows.map((row) => row.children[0]?.textContent)).toEqual(["1", "2", "3", "4", "5", "6"]);
    expect(rows.map((row) => row.children[1]?.textContent)).toEqual([
      "1",
      "1.1",
      "1.1.01",
      "1.1.01.001",
      "1.1.01.002",
      "1.1.02",
    ]);
    expect(screen.queryByText("L Caja chica")).toBeNull();
    expect(screen.queryByText(/[\u2502\u2514\u251c]/)).toBeNull();

    const nameCells = rows.map((row) => row.children[2]?.firstElementChild as HTMLElement);
    expect(nameCells.map((cell) => cell.textContent)).toEqual([
      "Activo",
      "Activo corriente",
      "Efectivo y equivalentes",
      "Caja General",
      "Caja chica",
      "Bancos",
    ]);
    expect(nameCells.map((cell) => cell.dataset.depth)).toEqual(["0", "1", "2", "3", "3", "2"]);
    expect(nameCells[0].querySelector(".coa-tree-name__guides")).toBeNull();

    const depthOneGuides = nameCells[1].querySelector(".coa-tree-name__guides");
    const depthThreeGuides = nameCells[3].querySelector(".coa-tree-name__guides");
    expect(depthOneGuides?.getAttribute("aria-hidden")).toBe("true");
    expect(depthOneGuides?.querySelectorAll(".coa-tree-name__guide")).toHaveLength(1);
    expect(depthThreeGuides?.getAttribute("aria-hidden")).toBe("true");
    expect(depthThreeGuides?.querySelectorAll(".coa-tree-name__guide")).toHaveLength(3);
    expect(depthThreeGuides?.textContent).toBe("");

    rows.forEach((row) => {
      expect(row.querySelector('button[aria-label="Editar"]')).toBeTruthy();
      expect(row.querySelector('button[aria-label="Desactivar"]')).toBeTruthy();
    });
  });

  it("pide confirmación antes de desactivar, explicando el impacto en asientos/reglas de posteo", async () => {
    vi.mocked(accountingApi.disableAccount).mockResolvedValue(ACTIVE_ACCOUNT);

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(accountingApi.disableAccount).toHaveBeenCalledWith("acc-1");
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/no podrá usarse para nuevos asientos/i);
    expect(String(options.message)).toMatch(/no se eliminan/i);
    expect(String(options.message)).toMatch(/reglas contables/i);
  });

  it("si se cancela, no llama a disableAccount", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(accountingApi.disableAccount).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente muestra message.success", async () => {
    vi.mocked(accountingApi.disableAccount).mockResolvedValue(ACTIVE_ACCOUNT);

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Cuenta desactivada correctamente."),
    );
  });

  it("si falla, muestra el mensaje real del backend y no muestra éxito", async () => {
    vi.mocked(accountingApi.disableAccount).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "La cuenta tiene reglas de posteo activas." } },
      },
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() =>
      expect(message.error).toHaveBeenCalledWith(
        "La cuenta tiene reglas de posteo activas.",
      ),
    );
    expect(message.success).not.toHaveBeenCalled();
  });

  it("activar explica que vuelve a estar disponible para uso contable", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue([
      { ...ACTIVE_ACCOUNT, isActive: false },
    ]);
    vi.mocked(accountingApi.enableAccount).mockResolvedValue(ACTIVE_ACCOUNT);

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Activar" }));

    await waitFor(() => expect(accountingApi.enableAccount).toHaveBeenCalledWith("acc-1"));
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/volverá a estar disponible para uso contable/i);
    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Cuenta activada correctamente."),
    );
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(accountingApi.disableAccount).mockResolvedValue(ACTIVE_ACCOUNT);

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));
    await waitFor(() => expect(accountingApi.disableAccount).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});

/**
 * ACCOUNTING-CHART-LIST-INTERACTIVITY-01 — columna N°, resumen compacto, texto "Mostrando X de Y",
 * chips rápidos y filtro por nivel. 100% cálculo/estado en frontend sobre la lista ya cargada — sin
 * tocar backend/DTOs/contratos API.
 */
const INTERACTIVITY_ACCOUNTS = [
  {
    ...ACTIVE_ACCOUNT,
    id: "acc-root",
    code: "1",
    name: "Activo",
    level: 0,
    allowsPosting: false,
    isActive: true,
  },
  {
    ...ACTIVE_ACCOUNT,
    id: "acc-mid",
    code: "1.1",
    name: "Activo corriente",
    level: 1,
    allowsPosting: false,
    isActive: true,
  },
  {
    ...ACTIVE_ACCOUNT,
    id: "acc-leaf-1",
    code: "1.1.01",
    name: "Caja general",
    level: 2,
    allowsPosting: true,
    isActive: true,
  },
  {
    ...ACTIVE_ACCOUNT,
    id: "acc-leaf-2",
    code: "1.1.02",
    name: "Bancos",
    level: 2,
    allowsPosting: true,
    isActive: false,
  },
];

describe("ChartOfAccountsPage — interactividad del listado (N°, resumen, chips, nivel)", () => {
  it("renderiza columna N° con índice iniciando en 1 según el orden visible", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue(INTERACTIVITY_ACCOUNTS);

    const { container } = renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    expect(screen.getByText("N°")).toBeTruthy();
    const rows = Array.from(container.querySelectorAll("tbody tr"));
    expect(rows.map((row) => row.children[0]?.textContent)).toEqual(["1", "2", "3", "4"]);
    // El N° es un índice visual, no el Id — nunca coincide con acc-root/acc-mid/etc.
    expect(rows[0]?.children[0]?.textContent).not.toContain("acc-");
  });

  it("renderiza el resumen compacto (Total/Agrupadoras/Movimiento/Activas/Inactivas/Nivel máximo)", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue(INTERACTIVITY_ACCOUNTS);

    const { container } = renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    // Scoped a .pg-kpis: "Activas"/"Inactivas" también existen como chips rápidos (botones),
    // así que buscar en todo el documento sería ambiguo.
    const kpis = container.querySelector(".pg-kpis") as HTMLElement;
    const getKpiValue = (label: string) => {
      const labelEl = Array.from(kpis.querySelectorAll(".pg-kpi-label")).find(
        (el) => el.textContent === label,
      );
      return labelEl?.closest(".pg-kpi")?.querySelector(".pg-kpi-value")?.textContent?.trim();
    };

    // 4 cuentas totales, 2 agrupadoras (allowsPosting=false), 2 de movimiento,
    // 3 activas, 1 inactiva, nivel máximo 2.
    expect(getKpiValue("Total cuentas")).toBe("4");
    expect(getKpiValue("Agrupadoras")).toBe("2");
    expect(getKpiValue("Movimiento")).toBe("2");
    expect(getKpiValue("Activas")).toBe("3");
    expect(getKpiValue("Inactivas")).toBe("1");
    expect(getKpiValue("Nivel máximo")).toBe("2");
  });

  it('renderiza "Mostrando X de Y cuentas" y X baja al filtrar', async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue(INTERACTIVITY_ACCOUNTS);

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    expect(screen.getByText("Mostrando 4 de 4 cuentas")).toBeTruthy();

    fireEvent.change(screen.getByPlaceholderText("Buscar por código o nombre..."), {
      target: { value: "Bancos" },
    });

    await waitFor(() => expect(screen.getByText("Mostrando 1 de 4 cuentas")).toBeTruthy());
  });

  it("el chip rápido Agrupadoras filtra a allowsPosting=false y es accesible vía aria-pressed", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue(INTERACTIVITY_ACCOUNTS);

    const { container } = renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    const groupChip = screen.getByRole("button", { name: "Agrupadoras" });
    expect(groupChip.getAttribute("aria-pressed")).toBe("false");

    fireEvent.click(groupChip);

    expect(groupChip.getAttribute("aria-pressed")).toBe("true");
    await waitFor(() => expect(screen.getByText("Mostrando 2 de 4 cuentas")).toBeTruthy());
    const rows = Array.from(container.querySelectorAll("tbody tr"));
    expect(rows.map((row) => row.children[1]?.textContent)).toEqual(["1", "1.1"]);
  });

  it("el chip rápido Movimiento filtra a allowsPosting=true", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue(INTERACTIVITY_ACCOUNTS);

    const { container } = renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Movimiento" }));

    await waitFor(() => expect(screen.getByText("Mostrando 2 de 4 cuentas")).toBeTruthy());
    const rows = Array.from(container.querySelectorAll("tbody tr"));
    expect(rows.map((row) => row.children[1]?.textContent)).toEqual(["1.1.01", "1.1.02"]);
  });

  it('el chip "Todas" limpia el filtro rápido', async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue(INTERACTIVITY_ACCOUNTS);

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Movimiento" }));
    await waitFor(() => expect(screen.getByText("Mostrando 2 de 4 cuentas")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Todas" }));
    await waitFor(() => expect(screen.getByText("Mostrando 4 de 4 cuentas")).toBeTruthy());
  });

  it("el filtro por nivel usa row.level y se genera dinámicamente desde las cuentas cargadas", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue(INTERACTIVITY_ACCOUNTS);

    const { container } = renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    const levelSelect = screen.getByLabelText("Filtrar por nivel") as HTMLSelectElement;
    expect(Array.from(levelSelect.options).map((o) => o.textContent)).toEqual([
      "Todos los niveles",
      "Nivel 0",
      "Nivel 1",
      "Nivel 2",
    ]);

    fireEvent.change(levelSelect, { target: { value: "2" } });

    await waitFor(() => expect(screen.getByText("Mostrando 2 de 4 cuentas")).toBeTruthy());
    const rows = Array.from(container.querySelectorAll("tbody tr"));
    expect(rows.map((row) => row.children[1]?.textContent)).toEqual(["1.1.01", "1.1.02"]);
  });

  it("los filtros nuevos conviven con búsqueda/tipo/estado existentes sin romperlos", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue(INTERACTIVITY_ACCOUNTS);

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    // Chip "Activas" (nuevo) + select "Todos los estados" (existente, sin filtrar) — deben convivir.
    fireEvent.click(screen.getByRole("button", { name: "Activas" }));
    await waitFor(() => expect(screen.getByText("Mostrando 3 de 4 cuentas")).toBeTruthy());

    // Combinar con el select de estado existente ("Inactivas") — resultado vacío, sin romper.
    fireEvent.change(screen.getByText("Todos los estados").closest("select") as HTMLSelectElement, {
      target: { value: "inactive" },
    });
    await waitFor(() => expect(screen.getByText("Mostrando 0 de 4 cuentas")).toBeTruthy());
  });

  it("las acciones de fila y las guías jerárquicas siguen presentes tras los nuevos filtros", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue(INTERACTIVITY_ACCOUNTS);

    const { container } = renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    const rows = Array.from(container.querySelectorAll("tbody tr"));
    rows.forEach((row) => {
      expect(row.querySelector('button[aria-label="Editar"]')).toBeTruthy();
    });
    expect(container.querySelectorAll(".coa-tree-name__guides").length).toBeGreaterThan(0);
    expect(screen.queryByText(/[│└├]/)).toBeNull();
  });
});
