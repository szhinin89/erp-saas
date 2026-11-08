// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup, within } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { ChartOfAccountsPage } from "./ChartOfAccountsPage";
import { accountingApi } from "../api/accountingApi";
import { message } from "../../../lib/messages";
import { account, byId, CHART_ACCOUNTS, visibleCodes } from "./ChartOfAccountsPage.testFixtures";

/**
 * ACCOUNTING-CHART-UX-01 — listado jerárquico (5 columnas, expandir/contraer, contexto al filtrar,
 * vista plana por nivel, filtros únicos, 4 KPI, permisos visibles) + regresión de
 * CRITICAL-CONFIRMATIONS-INVENTORY-ACCOUNTING-05 (confirmación de activar/desactivar).
 * Crear/editar en modal: ChartOfAccountsPage.modal.test.tsx.
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

vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));

vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));

function grant(...keys: string[] | ["*"]) {
  const has = (key: string) => keys[0] === "*" || (keys as string[]).includes(key);
  vi.mocked(usePermissionsUi).mockReturnValue({ canShow: has, has, isAdminRole: false });
}

function renderPage() {
  return render(
    <I18nProvider>
      <ChartOfAccountsPage />
    </I18nProvider>,
  );
}

async function renderLoaded() {
  const view = renderPage();
  await waitFor(() => expect(screen.getByText("Activo corriente")).toBeTruthy());
  return view;
}

const CAJA = account({ id: "acc-1", code: "1.1.01", name: "Caja general" });

beforeEach(() => {
  vi.clearAllMocks();
  grant("*");
  vi.mocked(accountingApi.listAccounts).mockResolvedValue(CHART_ACCOUNTS);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(() => cleanup());

describe("ChartOfAccountsPage — jerarquía y columnas", () => {
  it("muestra cinco columnas, sin N°, y arranca con los niveles 0 y 1 visibles", async () => {
    const { container } = await renderLoaded();

    const headers = Array.from(container.querySelectorAll("thead th")).map((th) => th.textContent);
    expect(headers).toEqual(["Cuenta", "Tipo / Naturaleza", "Clase", "Estado", "Acciones"]);
    expect(visibleCodes(container)).toEqual(["1", "1.1", "2", "2.1"]);
    expect(screen.queryByText("Caja general")).toBeNull();
    expect(screen.getByText("4 visibles · 6 cuentas en total")).toBeTruthy();
  });

  it("distingue agrupadoras y de movimiento, y agrupa tipo con naturaleza", async () => {
    const { container } = await renderLoaded();
    const rows = Array.from(container.querySelectorAll("tbody tr"));

    expect(within(rows[0] as HTMLElement).getByText("Agrupadora")).toBeTruthy();
    expect(within(rows[3] as HTMLElement).getByText("Movimiento")).toBeTruthy();
    expect(rows[3]?.children[1]?.textContent).toBe("PasivoCrédito");
    expect(rows[0]?.querySelector(".acc-coa-tree-name--group")).toBeTruthy();
    expect(rows[3]?.querySelector(".acc-coa-tree-name--group")).toBeNull();
  });

  it("expande y contrae una cuenta con aria-expanded y nombre accesible", async () => {
    const { container } = await renderLoaded();

    const toggle = screen.getByRole("button", { name: "Expandir 1.1 Activo corriente (2 subcuentas)" });
    expect(toggle.getAttribute("aria-expanded")).toBe("false");
    fireEvent.click(toggle);

    expect(visibleCodes(container)).toEqual(["1", "1.1", "1.1.01", "1.1.02", "2", "2.1"]);
    const collapse = screen.getByRole("button", { name: "Contraer 1.1 Activo corriente (2 subcuentas)" });
    expect(collapse.getAttribute("aria-expanded")).toBe("true");

    fireEvent.click(screen.getByRole("button", { name: "Contraer 1 Activo (1 subcuentas)" }));
    expect(visibleCodes(container)).toEqual(["1", "2", "2.1"]);
  });

  it("Expandir todo y Contraer todo", async () => {
    const { container } = await renderLoaded();

    fireEvent.click(screen.getByRole("button", { name: /Expandir todo/ }));
    expect(visibleCodes(container)).toHaveLength(6);

    fireEvent.click(screen.getByRole("button", { name: /Contraer todo/ }));
    expect(visibleCodes(container)).toEqual(["1", "2"]);
  });

  it("una cuenta con padre no disponible se muestra como raíz y queda señalada", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue([
      ...CHART_ACCOUNTS,
      account({ id: "orph", code: "9.1", name: "Huérfana", parentAccountId: "missing" }),
    ]);
    const { container } = await renderLoaded();

    expect(visibleCodes(container)).toContain("9.1");
    expect(screen.getByText("(padre no disponible)")).toBeTruthy();
  });

  it("muestra cuatro indicadores: Total, Agrupadoras, Movimiento e Inactivas", async () => {
    const { container } = await renderLoaded();
    const kpis = container.querySelector(".pg-kpis") as HTMLElement;
    const values = Object.fromEntries(
      Array.from(kpis.querySelectorAll(".pg-kpi")).map((kpi) => [
        kpi.querySelector(".pg-kpi-label")?.textContent,
        kpi.querySelector(".pg-kpi-value")?.textContent?.trim(),
      ]),
    );
    expect(values).toEqual({ "Total cuentas": "6", Agrupadoras: "3", Movimiento: "3", Inactivas: "1" });
  });
});

describe("ChartOfAccountsPage — contador del listado", () => {
  const counter = (container: HTMLElement) =>
    container.querySelector(".acc-coa-list-summary-text")?.textContent;

  it("distingue visibles, coincidencias, contexto y total al expandir, contraer, buscar y limpiar", async () => {
    const { container } = await renderLoaded();
    // Contraído inicial (niveles 0 y 1): 4 filas visibles de 6 cuentas.
    expect(counter(container)).toBe("4 visibles · 6 cuentas en total");

    fireEvent.click(screen.getByRole("button", { name: "Expandir 1.1 Activo corriente (2 subcuentas)" }));
    expect(counter(container)).toBe("6 visibles · 6 cuentas en total");

    fireEvent.click(screen.getByRole("button", { name: /Contraer todo/ }));
    expect(counter(container)).toBe("2 visibles · 6 cuentas en total");

    fireEvent.click(screen.getByRole("button", { name: /Expandir todo/ }));
    expect(counter(container)).toBe("6 visibles · 6 cuentas en total");

    fireEvent.change(screen.getByPlaceholderText("Código o nombre..."), { target: { value: "caja" } });
    expect(counter(container)).toBe("1 coincidencia · 2 cuentas de contexto · 6 cuentas en total");

    fireEvent.change(screen.getByPlaceholderText("Código o nombre..."), { target: { value: "activo" } });
    // "Activo" y "Activo corriente" coinciden; ninguna fila es solo contexto.
    expect(counter(container)).toBe("2 coincidencias · 6 cuentas en total");

    fireEvent.click(screen.getByRole("button", { name: /Limpiar filtros/ }));
    // Al limpiar vuelve al estado de expansión previo (todo expandido).
    expect(counter(container)).toBe("6 visibles · 6 cuentas en total");
  });

  it("es una región aria-live para anunciar los cambios", async () => {
    const { container } = await renderLoaded();
    expect(container.querySelector(".acc-coa-list-summary-text")?.getAttribute("aria-live")).toBe("polite");
  });
});

describe("ChartOfAccountsPage — búsqueda y filtros", () => {
  it("la búsqueda muestra coincidencias con sus ancestros de contexto, aunque estén contraídos", async () => {
    const { container } = await renderLoaded();

    fireEvent.change(screen.getByPlaceholderText("Código o nombre..."), { target: { value: "bancos" } });

    expect(visibleCodes(container)).toEqual(["1", "1.1", "1.1.02"]);
    const rows = Array.from(container.querySelectorAll("tbody tr"));
    expect(rows.map((r) => r.classList.contains("acc-coa-row--context"))).toEqual([true, true, false]);
    expect(screen.getAllByText("(contexto)")).toHaveLength(2);
    expect(screen.getByText("1 coincidencia · 2 cuentas de contexto · 6 cuentas en total")).toBeTruthy();
    // Con filtros activos no hay botones de expandir ni acciones de árbol habilitadas.
    expect(screen.queryByRole("button", { name: /^Expandir 1\.1/ })).toBeNull();
    expect((screen.getByRole("button", { name: /Expandir todo/ }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("filtros de clase y estado combinados, con contexto y botón Limpiar filtros", async () => {
    const { container } = await renderLoaded();

    fireEvent.change(screen.getByLabelText("Clase"), { target: { value: "posting" } });
    fireEvent.change(screen.getByLabelText("Estado"), { target: { value: "active" } });

    expect(visibleCodes(container)).toEqual(["1", "1.1", "1.1.01", "2", "2.1"]);
    expect(screen.getByText("2 coincidencias · 3 cuentas de contexto · 6 cuentas en total")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: /Limpiar filtros/ }));
    expect(visibleCodes(container)).toEqual(["1", "1.1", "2", "2.1"]);
    expect(screen.queryByRole("button", { name: /Limpiar filtros/ })).toBeNull();
  });

  it("el filtro por tipo conserva la jerarquía", async () => {
    const { container } = await renderLoaded();
    fireEvent.change(screen.getByLabelText("Tipo"), { target: { value: "Liability" } });
    expect(visibleCodes(container)).toEqual(["2", "2.1"]);
  });

  it("el filtro por nivel usa row.level y presenta una vista plana", async () => {
    const { container } = await renderLoaded();
    const level = screen.getByLabelText("Nivel") as HTMLSelectElement;
    expect(Array.from(level.options).map((o) => o.textContent)).toEqual([
      "Todos (jerarquía)",
      "Nivel 0 (lista plana)",
      "Nivel 1 (lista plana)",
      "Nivel 2 (lista plana)",
    ]);

    fireEvent.change(level, { target: { value: "2" } });

    expect(visibleCodes(container)).toEqual(["1.1.01", "1.1.02"]);
    expect(container.querySelector(".acc-coa-tree-name__guides")).toBeNull();
    expect(screen.queryByText("(contexto)")).toBeNull();
    expect(screen.getByText("2 coincidencias · 6 cuentas en total")).toBeTruthy();
  });

  it("sin coincidencias muestra un mensaje propio de filtros", async () => {
    await renderLoaded();
    fireEvent.change(screen.getByPlaceholderText("Código o nombre..."), { target: { value: "zzz" } });
    expect(screen.getByText("Ninguna cuenta coincide con los filtros.")).toBeTruthy();
  });
});

describe("ChartOfAccountsPage — permisos visibles", () => {
  it("sin permisos de escritura no muestra Nueva cuenta ni acciones de fila", async () => {
    grant("accounting.view");
    const { container } = await renderLoaded();

    expect(screen.queryByRole("button", { name: /Nueva cuenta/ })).toBeNull();
    expect(container.querySelectorAll("tbody .prd-row-actions button")).toHaveLength(0);
  });

  it("accounting.update edita y activa, pero desactivar exige accounting.delete", async () => {
    grant("accounting.update");
    await renderLoaded();
    fireEvent.click(screen.getByRole("button", { name: /Expandir todo/ }));

    expect(screen.getByRole("button", { name: "Editar 1.1.01 Caja general" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Activar 1.1.02 Bancos" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Desactivar 1.1.01 Caja general" })).toBeNull();
    expect(screen.queryByRole("button", { name: /Crear subcuenta/ })).toBeNull();
    expect(screen.queryByRole("button", { name: /Nueva cuenta/ })).toBeNull();
  });

  it("accounting.create habilita Nueva cuenta y Crear subcuenta solo en agrupadoras activas", async () => {
    grant("accounting.create");
    await renderLoaded();

    expect(screen.getByRole("button", { name: /Nueva cuenta/ })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Crear subcuenta de 1.1 Activo corriente" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Crear subcuenta de 2.1 Proveedores" })).toBeNull();
    expect(screen.queryByRole("button", { name: /^Editar/ })).toBeNull();
  });
});

describe("ChartOfAccountsPage — activar/desactivar: confirmación y feedback", () => {
  beforeEach(() => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue([CAJA]);
  });

  const clickDisable = async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "Desactivar 1.1.01 Caja general" }));
  };

  it("pide confirmación antes de desactivar, explicando el impacto en asientos/reglas de posteo", async () => {
    vi.mocked(accountingApi.disableAccount).mockResolvedValue(CAJA);
    await clickDisable();

    await waitFor(() => expect(accountingApi.disableAccount).toHaveBeenCalledWith("acc-1"));
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/no podrá usarse para nuevos asientos/i);
    expect(String(options.message)).toMatch(/no se eliminan/i);
    expect(String(options.message)).toMatch(/reglas contables/i);
  });

  it("si se cancela, no llama a disableAccount", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    await clickDisable();
    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(accountingApi.disableAccount).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente muestra message.success y recarga", async () => {
    vi.mocked(accountingApi.disableAccount).mockResolvedValue(CAJA);
    await clickDisable();
    await waitFor(() => expect(message.success).toHaveBeenCalledWith("Cuenta desactivada correctamente."));
    expect(accountingApi.listAccounts).toHaveBeenCalledTimes(2);
  });

  it("si falla, muestra el mensaje real del backend y no muestra éxito", async () => {
    vi.mocked(accountingApi.disableAccount).mockRejectedValue({
      isAxiosError: true,
      response: { status: 409, data: { message: { user: "La cuenta tiene reglas de posteo activas." } } },
    });
    await clickDisable();
    await waitFor(() => expect(message.error).toHaveBeenCalledWith("La cuenta tiene reglas de posteo activas."));
    expect(message.success).not.toHaveBeenCalled();
  });

  it("activar explica que vuelve a estar disponible para uso contable", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue([{ ...CAJA, isActive: false }]);
    vi.mocked(accountingApi.enableAccount).mockResolvedValue(CAJA);
    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Activar 1.1.01 Caja general" }));

    await waitFor(() => expect(accountingApi.enableAccount).toHaveBeenCalledWith("acc-1"));
    expect(String(vi.mocked(message.confirm).mock.calls[0][0].message)).toMatch(
      /volverá a estar disponible para uso contable/i,
    );
    await waitFor(() => expect(message.success).toHaveBeenCalledWith("Cuenta activada correctamente."));
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const spies = [
      vi.spyOn(window, "confirm").mockReturnValue(true),
      vi.spyOn(window, "prompt").mockReturnValue(""),
      vi.spyOn(window, "alert").mockImplementation(() => {}),
    ];
    vi.mocked(accountingApi.disableAccount).mockResolvedValue(CAJA);
    await clickDisable();
    await waitFor(() => expect(accountingApi.disableAccount).toHaveBeenCalled());
    spies.forEach((spy) => {
      expect(spy).not.toHaveBeenCalled();
      spy.mockRestore();
    });
  });

  it("los nombres limpios no arrastran prefijos de árbol heredados", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue([
      { ...byId("a1"), name: "│ ├ Activo" },
      { ...byId("c11"), name: "L Activo corriente" },
    ]);
    renderPage();
    await waitFor(() => expect(screen.getByText("Activo corriente")).toBeTruthy());
    expect(screen.queryByText(/[│└├]/)).toBeNull();
    expect(screen.queryByText("L Activo corriente")).toBeNull();
  });
});
