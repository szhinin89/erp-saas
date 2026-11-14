// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { useAuthStore } from "../../../store/authStore";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { ExpenseDocumentFormPage } from "./ExpenseDocumentFormPage";
import { expenseDocumentService } from "../api/expenseDocumentService";
import type { ExpenseDocumentDetailDto } from "../api/expenseDocumentService";
import type { ExpenseCategoryTreeNodeDto } from "../api/expenseCategoryService";
import { purchaseReceptionExpenseFacade } from "../../purchases/facades/purchaseReceptionExpenseFacade";
import { supplierExpenseSubcategoryLookupFacade } from "../../masterData/facades/supplierExpenseSubcategoryLookupFacade";
import type { SupplierExpenseSubcategoriesDto } from "../../masterData/facades/supplierExpenseSubcategoryLookupFacade";

/**
 * EXPENSE-SUPPLIER-SUBCATEGORIES-01 (fase 3) — el formulario de gastos aplica las subcategorías
 * permitidas del proveedor (manual y XML): tres modos, autoselección, cambio de proveedor, borradores
 * existentes (D3/D7), cambio de empresa, permisos y errores del servidor. Editor y selector reales.
 */

vi.mock(
  "../../masterData/facades/supplierExpenseSubcategoryLookupFacade",
  () => ({
    supplierExpenseSubcategoryLookupFacade: { get: vi.fn() },
  }),
);
vi.mock("../../purchases/facades/purchaseReceptionExpenseFacade", () => ({
  purchaseReceptionExpenseFacade: { prepareExpenseDraft: vi.fn() },
}));
vi.mock("../api/expenseDocumentService", () => ({
  expenseDocumentService: {
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    confirm: vi.fn(),
    cancel: vi.fn(),
  },
}));
vi.mock("../api/expenseCategoryService", () => ({
  expenseCategoryService: { getTree: vi.fn() },
}));
vi.mock("../../masterData/facades/paymentTermLookupFacade", () => ({
  paymentTermLookupFacade: { list: async () => [] },
}));
vi.mock("../../items/facades/sriLookupFacade", () => ({
  sriLookupFacade: {
    docTypes: async () => [
      { code: "01", name: "Factura", shortName: "FAC", isElectronic: true },
    ],
    taxSupportCodes: async () => [],
    vatRates: async () => [{ code: "2", name: "15% IVA", percentage: 15 }],
  },
}));
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));
vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    info: vi.fn(),
    confirm: vi.fn(),
  },
}));
vi.mock("../components/ExpenseRetentionSection", () => ({
  ExpenseRetentionSection: () => null,
}));
// Elegir proveedor es responsabilidad de SupplierSearchSelect; aquí solo importa su efecto en el formulario.
vi.mock("../components/ExpenseDocumentHeader", () => ({
  ExpenseDocumentHeader: ({
    value,
    onChange,
    onSupplierChange,
  }: {
    value: { supplierId: string };
    onChange: (patch: { supplierId: string }) => void;
    onSupplierChange: (row: unknown) => void;
  }) => (
    <div>
      <span data-testid="supplier">{value.supplierId}</span>
      {["sup-1", "sup-2"].map((id) => (
        <button
          key={id}
          onClick={() => {
            onSupplierChange({
              id,
              fullName: id,
              identificationNumber: "1",
              isActive: true,
              hasSupplierRole: true,
              supplierConfig: null,
            });
            onChange({ supplierId: id });
          }}
        >
          {`Elegir ${id}`}
        </button>
      ))}
    </div>
  ),
}));

const node = (
  id: string,
  level: "Type" | "Category" | "Subcategory",
  children: ExpenseCategoryTreeNodeDto[] = [],
): ExpenseCategoryTreeNodeDto => ({
  id,
  companyId: "c",
  parentId: null,
  code: id.toUpperCase(),
  name: `Nombre ${id}`,
  description: null,
  level,
  accountingAccountId: level === "Subcategory" ? "acc" : null,
  accountingAccountCode: level === "Subcategory" ? "5.1.1" : null,
  accountingAccountName: level === "Subcategory" ? "Servicios" : null,
  isActive: true,
  children,
});

const TREE = [
  node("t1", "Type", [
    node("c1", "Category", [
      node("s1", "Subcategory"),
      node("s2", "Subcategory"),
    ]),
  ]),
  node("t2", "Type", [node("c2", "Category", [node("s3", "Subcategory")])]),
];

const dto = (
  mode: SupplierExpenseSubcategoriesDto["mode"],
  available: string[] = [],
): SupplierExpenseSubcategoriesDto => ({
  mode,
  items: [],
  availableSubcategoryIds: available,
});
const configByPartner = new Map<string, SupplierExpenseSubcategoriesDto>();

const DRAFT = {
  id: "exp-1",
  companyId: "c",
  branchId: "b",
  supplierId: "sup-1",
  supplierName: "sup-1",
  supplierTaxId: "1",
  issueDate: "2026-09-01",
  accountingDate: "2026-09-01",
  documentType: "01",
  documentNumber: "001-001-000000001",
  authorizationNumber: null,
  authorizationDate: null,
  paymentTermId: null,
  paymentTermName: null,
  dueDate: null,
  subtotal: 100,
  totalDiscount: 0,
  totalTax: 15,
  grandTotal: 115,
  notes: null,
  taxSupportCode: null,
  status: "Draft",
  lines: [
    {
      id: "line-1",
      expenseSubcategoryId: "s3",
      description: "Servicio",
      quantity: 1,
      unitAmount: 100,
      discountAmount: 0,
      vatCode: "2",
      vatRate: 15,
      vatAmount: 15,
      taxInclusiveTotal: 115,
      sortOrder: 1,
      notes: null,
    },
  ],
} as unknown as ExpenseDocumentDetailDto;

function show(path: string) {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route
            path="/expenses/documents/new"
            element={<ExpenseDocumentFormPage />}
          />
          <Route
            path="/expenses/documents/:id"
            element={<ExpenseDocumentFormPage />}
          />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

const combo = (name: string, index = 0) =>
  screen.getAllByRole("combobox", { name: new RegExp(`^${name}`) })[
    index
  ] as HTMLSelectElement;
const optionValues = (select: HTMLSelectElement) =>
  within(select)
    .getAllByRole("option")
    .map((o) => (o as HTMLOptionElement).value)
    .filter(Boolean);
const saveButton = () =>
  screen.getByRole("button", { name: "Guardar borrador" }) as HTMLButtonElement;

async function chooseSupplier(id: string) {
  fireEvent.click(await screen.findByRole("button", { name: `Elegir ${id}` }));
}

function pick(typeId: string, categoryId: string, subcategoryId: string) {
  fireEvent.change(combo("Tipo de gasto"), { target: { value: typeId } });
  fireEvent.change(combo("Categoría"), { target: { value: categoryId } });
  fireEvent.change(combo("Subcategoría"), { target: { value: subcategoryId } });
}

beforeEach(async () => {
  vi.clearAllMocks();
  configByPartner.clear();
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
  vi.mocked(supplierExpenseSubcategoryLookupFacade.get).mockImplementation(
    async (id: string) => configByPartner.get(id) ?? dto("Unconfigured"),
  );
  const { expenseCategoryService } =
    await import("../api/expenseCategoryService");
  vi.mocked(expenseCategoryService.getTree).mockResolvedValue(TREE);
  vi.mocked(expenseDocumentService.getById).mockResolvedValue(DRAFT);
  vi.mocked(expenseDocumentService.update).mockResolvedValue(DRAFT);
});
afterEach(cleanup);

describe("ExpenseDocumentFormPage — subcategorías permitidas del proveedor", () => {
  it("Unconfigured: ofrece todo el catálogo", async () => {
    show("/expenses/documents/new");
    await chooseSupplier("sup-1");
    await waitFor(() =>
      expect(supplierExpenseSubcategoryLookupFacade.get).toHaveBeenCalledWith(
        "sup-1",
      ),
    );
    await waitFor(() =>
      expect(optionValues(combo("Tipo de gasto"))).toEqual(["t1", "t2"]),
    );
    fireEvent.change(combo("Tipo de gasto"), { target: { value: "t1" } });
    fireEvent.change(combo("Categoría"), { target: { value: "c1" } });
    expect(optionValues(combo("Subcategoría"))).toEqual(["s1", "s2"]);
  });

  it("Restricted con varias: solo las autorizadas, sin tipos ni categorías vacíos", async () => {
    configByPartner.set("sup-1", dto("Restricted", ["s1", "s2"]));
    show("/expenses/documents/new");
    await chooseSupplier("sup-1");
    await waitFor(() =>
      expect(optionValues(combo("Tipo de gasto"))).toEqual(["t1"]),
    );
    expect(combo("Subcategoría").value).toBe("");
    fireEvent.change(combo("Tipo de gasto"), { target: { value: "t1" } });
    fireEvent.change(combo("Categoría"), { target: { value: "c1" } });
    expect(optionValues(combo("Subcategoría"))).toEqual(["s1", "s2"]);
  });

  it("Restricted con una sola: se selecciona sola en la línea existente y en las nuevas", async () => {
    configByPartner.set("sup-1", dto("Restricted", ["s3"]));
    show("/expenses/documents/new");
    await chooseSupplier("sup-1");
    await waitFor(() => expect(combo("Subcategoría").value).toBe("s3"));
    fireEvent.click(screen.getByRole("button", { name: /Agregar concepto/ }));
    await waitFor(() =>
      expect(
        screen.getAllByRole("combobox", { name: /^Subcategoría/ }),
      ).toHaveLength(2),
    );
    expect(combo("Subcategoría", 1).value).toBe("s3");
  });

  it("RestrictedEmpty: avisa y no ofrece ninguna selección nueva", async () => {
    configByPartner.set("sup-1", dto("RestrictedEmpty"));
    show("/expenses/documents/new");
    await chooseSupplier("sup-1");
    expect(await screen.findByText(/ninguna está disponible/)).toBeTruthy();
    expect(optionValues(combo("Tipo de gasto"))).toEqual([]);
    expect(combo("Subcategoría").value).toBe("");
  });

  it("cambio de proveedor: limpia con aviso la selección nueva incompatible y asigna la única permitida", async () => {
    configByPartner.set("sup-2", dto("Restricted", ["s1"]));
    show("/expenses/documents/new");
    await chooseSupplier("sup-1");
    await waitFor(() =>
      expect(optionValues(combo("Tipo de gasto"))).toEqual(["t1", "t2"]),
    );
    pick("t2", "c2", "s3");
    expect(combo("Subcategoría").value).toBe("s3");

    await chooseSupplier("sup-2");
    await waitFor(() => expect(combo("Subcategoría").value).toBe("s1"));
    expect(await screen.findByText(/Se limpiaron 1 subcategoría/)).toBeTruthy();
  });

  it("borrador existente: abrirlo no modifica sus líneas aunque estén fuera del conjunto; guardar sigue permitido (D3/D7)", async () => {
    configByPartner.set("sup-1", dto("Restricted", ["s1"]));
    show("/expenses/documents/exp-1");
    expect(
      await screen.findByText(/ya estaba guardada y no está permitida/),
    ).toBeTruthy();
    expect(combo("Subcategoría").value).toBe("s3");
    await waitFor(() => expect(saveButton().disabled).toBe(false));
    expect(
      screen.queryByText(/no está permitida para este proveedor\. Elige/),
    ).toBeNull();

    fireEvent.click(saveButton());
    await waitFor(() =>
      expect(expenseDocumentService.update).toHaveBeenCalled(),
    );
    const payload = vi.mocked(expenseDocumentService.update).mock.calls[0][1];
    expect(payload.lines[0].expenseSubcategoryId).toBe("s3");
  });

  it("borrador existente con otro proveedor: la línea guardada se conserva, se marca y bloquea guardar y confirmar", async () => {
    configByPartner.set("sup-2", dto("Restricted", ["s1"]));
    show("/expenses/documents/exp-1");
    await waitFor(() => expect(saveButton().disabled).toBe(false));

    await chooseSupplier("sup-2");
    expect(
      await screen.findByText(/no está permitida para este proveedor\. Elige/),
    ).toBeTruthy();
    expect(combo("Subcategoría").value).toBe("s3");
    await waitFor(() => expect(saveButton().disabled).toBe(true));
    expect(
      (screen.getByRole("button", { name: /Confirmar/ }) as HTMLButtonElement)
        .disabled,
    ).toBe(true);

    fireEvent.change(combo("Tipo de gasto"), { target: { value: "t1" } });
    fireEvent.change(combo("Categoría"), { target: { value: "c1" } });
    fireEvent.change(combo("Subcategoría"), { target: { value: "s1" } });
    await waitFor(() => expect(saveButton().disabled).toBe(false));
    expect(
      (screen.getByRole("button", { name: /Confirmar/ }) as HTMLButtonElement)
        .disabled,
    ).toBe(false);
  });

  it("recepción XML: el proveedor del comprobante recibe el mismo comportamiento", async () => {
    configByPartner.set("supplier-xml", dto("Restricted", ["s2"]));
    vi.mocked(
      purchaseReceptionExpenseFacade.prepareExpenseDraft,
    ).mockResolvedValue({
      receptionDocumentId: "r1",
      accessKey: "1".repeat(49),
      supplierId: "supplier-xml",
      supplierName: "Proveedor XML",
      supplierTaxId: "1790012345001",
      issueDate: "2026-09-01",
      documentType: "01",
      documentNumber: "001-001-000000001",
      authorizationNumber: null,
      authorizationDate: null,
      subtotal: 100,
      vatAmount: 15,
      total: 115,
    } as never);
    show("/expenses/documents/new?fromReceptionId=r1");
    await waitFor(() =>
      expect(supplierExpenseSubcategoryLookupFacade.get).toHaveBeenCalledWith(
        "supplier-xml",
      ),
    );
    await waitFor(() => expect(combo("Subcategoría").value).toBe("s2"));
  });

  it("error de lectura: avisa, no ofrece el catálogo completo y bloquea guardar", async () => {
    vi.mocked(supplierExpenseSubcategoryLookupFacade.get).mockRejectedValue(
      new Error("403 sin acceso"),
    );
    show("/expenses/documents/new");
    await chooseSupplier("sup-1");
    expect(
      await screen.findByText(
        /No se pudieron leer las subcategorías permitidas/,
      ),
    ).toBeTruthy();
    expect(combo("Tipo de gasto").disabled).toBe(true);
    expect(saveButton().disabled).toBe(true);
  });

  it("cambio de empresa: vuelve a leer la configuración del proveedor", async () => {
    show("/expenses/documents/new");
    await chooseSupplier("sup-1");
    await waitFor(() =>
      expect(supplierExpenseSubcategoryLookupFacade.get).toHaveBeenCalledTimes(
        1,
      ),
    );
    configByPartner.set("sup-1", dto("Restricted", ["s1", "s2"]));
    act(() => {
      useAuthStore.setState({
        companySessionVersion:
          useAuthStore.getState().companySessionVersion + 1,
      });
    });
    await waitFor(() =>
      expect(supplierExpenseSubcategoryLookupFacade.get).toHaveBeenCalledTimes(
        2,
      ),
    );
    await waitFor(() =>
      expect(optionValues(combo("Tipo de gasto"))).toEqual(["t1"]),
    );
  });

  it("sin acceso al catálogo de gastos no se consulta la configuración", async () => {
    vi.mocked(usePermissionsUi).mockReturnValue({
      canShow: () => true,
      has: (key: string) => key !== "expenses.catalog.view",
      isAdminRole: false,
    });
    show("/expenses/documents/new");
    await chooseSupplier("sup-1");
    expect(supplierExpenseSubcategoryLookupFacade.get).not.toHaveBeenCalled();
  });

  it("si falla la lectura tras cambiar de empresa no reutiliza el alcance anterior", async () => {
    configByPartner.set("sup-1", dto("Restricted", ["s1"]));
    show("/expenses/documents/new");
    await chooseSupplier("sup-1");
    await waitFor(() => expect(combo("Subcategoría").value).toBe("s1"));
    vi.mocked(supplierExpenseSubcategoryLookupFacade.get).mockRejectedValue(
      new Error("Error al leer la nueva empresa"),
    );
    act(() => {
      useAuthStore.setState({
        companySessionVersion: useAuthStore.getState().companySessionVersion + 1,
      });
    });
    expect(await screen.findByText(/No se pudieron leer las subcategorías permitidas/)).toBeTruthy();
    expect(combo("Tipo de gasto").disabled).toBe(true);
    expect(saveButton().disabled).toBe(true);
  });
});
