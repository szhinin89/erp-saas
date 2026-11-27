// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";

import { AccountsReceivablePage } from "./AccountsReceivablePage";
import {
  receivableService,
  type SalesReceivableDto,
} from "../api/receivableService";
import { I18nProvider } from "../../../i18n/i18n";
import { formatDate } from "../../../lib/formatters/dateFormatters";

const access = vi.hoisted(() => ({ finance: false, reverse: false }));

vi.mock("../api/receivableService", () => ({
  receivableService: { list: vi.fn() },
}));
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({
    isAdminRole: false,
    canShow: () => access.finance,
  }),
}));
vi.mock("../../../store/sessionStore", () => ({
  useSessionStore: (select: (s: unknown) => unknown) =>
    select({
      authorization: {
        permissions: access.reverse ? ["finance.collections.reverse"] : [],
      },
    }),
}));
vi.mock("../components/RegisterCollectionModal", () => ({
  RegisterCollectionModal: ({
    receivable,
    onClose,
  }: {
    receivable: SalesReceivableDto | null;
    onClose: () => void;
  }) =>
    receivable ? (
      <div role="dialog" aria-modal="true">
        cobro:{receivable.id}
        <button type="button" onClick={onClose}>
          cerrar
        </button>
      </div>
    ) : null,
}));
vi.mock("../components/ReverseCollectionModal", () => ({
  ReverseCollectionModal: ({
    receivable,
  }: {
    receivable: SalesReceivableDto;
  }) => <div>reversa:{receivable.id}</div>,
}));
vi.mock("../components/ReceivableCustomerAdvanceModal", () => ({
  ReceivableCustomerAdvanceModal: ({
    receivable,
  }: {
    receivable: SalesReceivableDto;
  }) => <div>anticipos:{receivable.id}</div>,
}));

const ROW: SalesReceivableDto = {
  id: "r1",
  invoiceId: null,
  invoiceNumber: "001-001-000000101",
  customerId: "c1",
  customerName: "Comercial Andina",
  customerIdentification: "1790012345001",
  branchId: "b1",
  branchName: "Sucursal Norte",
  createdByUserId: null,
  createdByName: "Ana Pérez",
  invoiceIssueDate: "2026-10-01",
  invoiceCreatedAt: "2026-10-28T12:29:28Z",
  dueDate: "2026-11-15",
  originalAmount: 300,
  paidAmount: 120,
  balanceDue: 180,
  status: "pending",
  statusLabel: "Parcial",
  installmentCount: 1,
  overdueDays: null,
  installments: [],
  createdAt: "2026-10-28T12:29:28Z",
  updatedAt: null,
};

async function show(overrides: Partial<SalesReceivableDto> = {}) {
  vi.mocked(receivableService.list).mockResolvedValue({
    items: [{ ...ROW, ...overrides }],
    total: 1,
    page: 1,
    pageSize: 50,
  });
  render(
    <I18nProvider>
      <MemoryRouter>
        <AccountsReceivablePage />
      </MemoryRouter>
    </I18nProvider>,
  );
  return screen.findByRole("table");
}

const trigger = () =>
  screen.queryByRole("button", {
    name: `Acciones de factura ${ROW.invoiceNumber}`,
  });
const actions = () =>
  screen.queryAllByRole("menuitem").map((x) => x.textContent);

beforeEach(() => {
  access.finance = false;
  access.reverse = false;
});
afterEach(cleanup);

describe("AccountsReceivablePage — presentación agrupada", () => {
  it("muestra siete columnas sin perder información", async () => {
    const table = await show();

    expect(
      within(table)
        .getAllByRole("columnheader")
        .map((x) => x.textContent),
    ).toEqual([
      "N°",
      "Factura",
      "Cliente",
      "Sucursal",
      "Vencimiento",
      "Valores",
      "Acciones",
    ]);

    const cells = within(table).getAllByRole("cell");
    expect(cells[1].textContent).toContain(ROW.invoiceNumber);
    expect(cells[1].textContent).toContain(formatDate("2026-10-01"));
    expect(cells[2].textContent).toContain("Comercial Andina");
    expect(cells[2].textContent).toContain("1790012345001");
    expect(cells[3].textContent).toContain("Sucursal Norte");
    expect(cells[3].textContent).toContain("Emitido por: Ana Pérez");
    expect(cells[4].textContent).toContain(formatDate("2026-11-15"));
    expect(cells[4].textContent).toContain("Parcial");
    expect(cells[5].textContent).toMatch(
      /Original.*300.*Cobrado.*120.*Saldo pendiente.*180/,
    );
  });

  it("la celda del cliente integra lo que en móvil se oculta", async () => {
    const cells = within(await show()).getAllByRole("cell");
    const compact = cells[2].querySelector(".fin-receivables__compact")!;

    for (const text of [
      "Parcial",
      `Vence: ${formatDate("2026-11-15")}`,
      `Factura ${ROW.invoiceNumber}`,
      formatDate("2026-10-01"),
      "Sucursal Norte",
      "Emitido por: Ana Pérez",
    ])
      expect(compact.textContent).toContain(text);
    for (const index of [1, 3, 4])
      expect(cells[index].className).toContain("fin-receivables__col-wide");
  });

  it.each([
    [{}, false, false, ["Registrar cobro"]],
    [
      {},
      true,
      true,
      ["Registrar cobro", "Consultar / anular cobro", "Anticipos"],
    ],
    [
      { balanceDue: 0, status: "paid" },
      true,
      true,
      ["Consultar / anular cobro", "Anticipos"],
    ],
    [{ status: "cancelled" }, false, true, ["Anticipos"]],
  ])(
    "con %o, reversa=%s y finance.view=%s ofrece %j",
    async (row, reverse, finance, expected) => {
      access.reverse = reverse;
      access.finance = finance;
      await show(row);
      fireEvent.click(trigger()!);

      expect(actions()).toEqual(expected);
    },
  );

  it("sin acciones disponibles no muestra el menú", async () => {
    await show({ balanceDue: 0, status: "paid" });
    expect(trigger()).toBeNull();
  });

  it.each([
    [/Registrar cobro/, "cobro:r1"],
    [/Consultar \/ anular cobro/, "reversa:r1"],
    [/Anticipos/, "anticipos:r1"],
  ])(
    "la acción %s abre su flujo con la cuenta de la fila y cierra el menú",
    async (name, result) => {
      access.reverse = true;
      access.finance = true;
      await show();
      fireEvent.click(trigger()!);
      fireEvent.click(screen.getByRole("menuitem", { name }));

      expect(screen.getByText(result)).toBeTruthy();
      expect(screen.queryByRole("menu")).toBeNull();
    },
  );
});

describe("AccountsReceivablePage — foco y textos", () => {
  it("al cerrar el modal abierto desde el menú, el foco vuelve a la fila", async () => {
    await show();
    fireEvent.click(trigger()!);
    fireEvent.click(screen.getByRole("menuitem", { name: "Registrar cobro" }));
    // Asíncrono: el observador del hook debe ver el modal antes de que se cierre.
    const close = await screen.findByRole("button", { name: "cerrar" });
    close.focus();
    fireEvent.click(close);

    await waitFor(() => expect(document.activeElement).toBe(trigger()));
  });

  it("título, filtro y lista vacía salen de las traducciones", async () => {
    vi.mocked(receivableService.list).mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 50,
    });
    render(
      <I18nProvider>
        <MemoryRouter>
          <AccountsReceivablePage />
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(await screen.findByText("Sin cuentas por cobrar.")).toBeTruthy();
    expect(screen.getByText("Cuentas por cobrar")).toBeTruthy();
    expect(screen.getAllByRole("option").map((x) => x.textContent)).toEqual([
      "Pendientes",
      "Pagadas",
      "Anuladas",
      "Todas",
    ]);
  });
});
