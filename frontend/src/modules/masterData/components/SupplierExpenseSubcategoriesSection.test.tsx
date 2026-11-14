// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import type { SupplierExpenseSubcategoriesDto } from "../types/businessPartner.types";
import { SupplierExpenseSubcategoriesSection } from "./SupplierExpenseSubcategoriesSection";

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getExpenseSubcategories: vi.fn(),
    setExpenseSubcategories: vi.fn(),
  },
}));
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));

const catalog = [
  {
    expenseSubcategoryId: "s1",
    code: "AGUA",
    name: "Agua",
    categoryName: "Servicios",
    typeName: "Operativo",
  },
  {
    expenseSubcategoryId: "s2",
    code: "LUZ",
    name: "Luz",
    categoryName: "Servicios",
    typeName: "Operativo",
  },
];

const item = (
  id: string,
  name: string,
  isActive = true,
  isSubcategoryActive = true,
) => ({
  expenseSubcategoryId: id,
  code: name.toUpperCase().slice(0, 4),
  name,
  categoryName: "Servicios",
  typeName: "Operativo",
  isActive,
  isSubcategoryActive,
});

const unconfigured: SupplierExpenseSubcategoriesDto = {
  mode: "Unconfigured",
  items: [],
  availableSubcategoryIds: [],
  catalog,
};
const restricted: SupplierExpenseSubcategoriesDto = {
  mode: "Restricted",
  items: [item("s1", "Agua")],
  availableSubcategoryIds: ["s1"],
  catalog,
};

function allow(view: boolean, configure: boolean) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (p: string) =>
      p === "masterdata.businesspartners.view"
        ? view
        : p === "masterdata.businesspartners.configure-company"
          ? configure
          : false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
}

function mount() {
  return render(
    <I18nProvider>
      <SupplierExpenseSubcategoriesSection bpId="bp1" />
    </I18nProvider>,
  );
}

describe("SupplierExpenseSubcategoriesSection", () => {
  beforeEach(() => {
    vi.mocked(businessPartnerFacade.getExpenseSubcategories)
      .mockReset()
      .mockResolvedValue(unconfigured);
    vi.mocked(businessPartnerFacade.setExpenseSubcategories)
      .mockReset()
      .mockResolvedValue({ ...restricted, catalog: null });
    vi.mocked(message.confirm).mockReset().mockResolvedValue(true);
    vi.mocked(message.success).mockReset();
    allow(true, true);
  });
  afterEach(cleanup);

  it("sin permiso de lectura no renderiza ni consulta nada", () => {
    allow(false, false);
    const { container } = mount();
    expect(container.firstChild).toBeNull();
    expect(
      businessPartnerFacade.getExpenseSubcategories,
    ).not.toHaveBeenCalled();
  });

  it("muestra el catálogo que entrega el GET y guarda el conjunto elegido con PUT", async () => {
    mount();
    expect(await screen.findByText(/Sin restricción/)).toBeTruthy();
    expect(screen.getByLabelText("AGUA — Agua")).toBeTruthy();
    expect(
      screen.queryByRole("button", { name: "Quitar restricción" }),
    ).toBeNull();

    const save = screen.getByRole("button", { name: "Guardar subcategorías" });
    expect((save as HTMLButtonElement).disabled).toBe(true);
    fireEvent.click(screen.getByLabelText("AGUA — Agua"));
    fireEvent.click(screen.getByLabelText("LUZ — Luz"));
    fireEvent.click(save);

    await waitFor(() =>
      expect(
        businessPartnerFacade.setExpenseSubcategories,
      ).toHaveBeenCalledWith("bp1", {
        expenseSubcategoryIds: ["s1", "s2"],
      }),
    );
    expect(message.success).toHaveBeenCalled();
    expect(message.confirm).not.toHaveBeenCalled();
    expect(await screen.findByText(/Restringido/)).toBeTruthy();
    // El PUT no devuelve el catálogo: la edición sigue disponible con el del último GET.
    expect(screen.getByLabelText("LUZ — Luz")).toBeTruthy();
  });

  it("«Quitar restricción» pide confirmación y envía PUT []", async () => {
    vi.mocked(businessPartnerFacade.getExpenseSubcategories).mockResolvedValue(
      restricted,
    );
    vi.mocked(businessPartnerFacade.setExpenseSubcategories).mockResolvedValue(
      unconfigured,
    );
    mount();
    fireEvent.click(
      await screen.findByRole("button", { name: "Quitar restricción" }),
    );

    await waitFor(() =>
      expect(
        businessPartnerFacade.setExpenseSubcategories,
      ).toHaveBeenCalledWith("bp1", {
        expenseSubcategoryIds: [],
      }),
    );
    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(await screen.findByText(/Sin restricción/)).toBeTruthy();
  });

  it("si se cancela la confirmación no se envía nada", async () => {
    vi.mocked(businessPartnerFacade.getExpenseSubcategories).mockResolvedValue(
      restricted,
    );
    vi.mocked(message.confirm).mockResolvedValue(false);
    mount();
    fireEvent.click(
      await screen.findByRole("button", { name: "Quitar restricción" }),
    );
    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(
      businessPartnerFacade.setExpenseSubcategories,
    ).not.toHaveBeenCalled();
  });

  it("desmarcar todo y guardar equivale a quitar la restricción (con confirmación)", async () => {
    vi.mocked(businessPartnerFacade.getExpenseSubcategories).mockResolvedValue(
      restricted,
    );
    mount();
    fireEvent.click(await screen.findByLabelText("AGUA — Agua"));
    fireEvent.click(
      screen.getByRole("button", { name: "Guardar subcategorías" }),
    );
    await waitFor(() =>
      expect(
        businessPartnerFacade.setExpenseSubcategories,
      ).toHaveBeenCalledWith("bp1", {
        expenseSubcategoryIds: [],
      }),
    );
    expect(message.confirm).toHaveBeenCalledTimes(1);
  });

  it("RestrictedEmpty advierte y conserva visible la asignación no disponible", async () => {
    vi.mocked(businessPartnerFacade.getExpenseSubcategories).mockResolvedValue({
      mode: "RestrictedEmpty",
      items: [item("s9", "Retirada", true, false)],
      availableSubcategoryIds: [],
      catalog,
    });
    mount();
    expect(await screen.findByText(/ninguna está disponible/)).toBeTruthy();
    const orphan = screen.getByLabelText(
      /RETI — Retirada \(No disponible en el catálogo\)/,
    ) as HTMLInputElement;
    expect(orphan.checked).toBe(true);
    expect(
      screen.getByRole("button", { name: "Quitar restricción" }),
    ).toBeTruthy();
  });

  it("solo lectura (sin configure-company): lista las asignadas, sin controles y sin catálogo", async () => {
    allow(true, false);
    vi.mocked(businessPartnerFacade.getExpenseSubcategories).mockResolvedValue({
      ...restricted,
      catalog: null,
    });
    mount();
    expect(
      await screen.findByText(/Operativo › Servicios › AGUA — Agua/),
    ).toBeTruthy();
    expect(screen.queryByRole("checkbox")).toBeNull();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("aunque el cliente crea poder configurar, sin catalog en la respuesta no ofrece edición", async () => {
    vi.mocked(businessPartnerFacade.getExpenseSubcategories).mockResolvedValue({
      ...restricted,
      catalog: null,
    });
    mount();
    expect(
      await screen.findByText(/Operativo › Servicios › AGUA — Agua/),
    ).toBeTruthy();
    expect(screen.queryByRole("checkbox")).toBeNull();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("muestra el error del servidor al fallar el guardado y conserva la selección", async () => {
    vi.mocked(businessPartnerFacade.setExpenseSubcategories).mockRejectedValue(
      new Error("boom"),
    );
    mount();
    fireEvent.click(await screen.findByLabelText("AGUA — Agua"));
    fireEvent.click(
      screen.getByRole("button", { name: "Guardar subcategorías" }),
    );
    expect(await screen.findByText(/No se pudieron guardar|boom/)).toBeTruthy();
    expect(
      (screen.getByLabelText("AGUA — Agua") as HTMLInputElement).checked,
    ).toBe(true);
    expect(message.success).not.toHaveBeenCalled();
  });
});
