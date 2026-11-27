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
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import es from "../../../i18n/locales/es.json";
import en from "../../../i18n/locales/en.json";
import { isRouteAllowed } from "../../../components/RouteAccessGuard";
import type { MainMenuGroup } from "../../../components/useAppLayoutNavigation";
import {
  ADVANCE,
  receivable,
  resetCatalogs,
} from "../testing/customerAdvanceFixtures";
import type { CustomerAdvanceDto } from "../api/customerAdvanceService";

const granted = new Set<string>();

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({
    isAdminRole: false,
    canShow: (key: string) => granted.has(key),
    has: (key: string) => granted.has(key),
  }),
}));
vi.mock("../api/customerAdvanceService", () => ({
  customerAdvanceService: {
    list: vi.fn(),
    getById: vi.fn(),
    balance: vi.fn(),
    byReceivable: vi.fn(),
    refund: vi.fn(),
    apply: vi.fn(),
  },
}));
vi.mock("../api/receivableService", () => ({
  receivableService: { list: vi.fn() },
}));
vi.mock("../api/bankAccountService", () => ({
  bankAccountService: { list: vi.fn() },
}));
vi.mock("../../sales/facades/paymentMethodLookupFacade", () => ({
  paymentMethodLookupFacade: { list: vi.fn() },
}));
vi.mock("../../caja/facades/cashRegisterLookupFacade", () => ({
  cashRegisterLookupFacade: { getCashRegisters: vi.fn() },
}));
vi.mock("../../session/facades/sessionBranchFacade", () => ({
  sessionBranchFacade: { listAvailable: vi.fn() },
}));
vi.mock("../../sales/facades/customerPickerFacade", () => ({
  CustomerPicker: ({ onChange }: { onChange: (c: { id: string }) => void }) => (
    <button type="button" onClick={() => onChange({ id: "cust-1" })}>
      elegir cliente
    </button>
  ),
}));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));

import { message } from "../../../lib/messages";
import { customerAdvanceService } from "../api/customerAdvanceService";
import { receivableService } from "../api/receivableService";
import { AccountsReceivablePage } from "./AccountsReceivablePage";
import { CustomerAdvanceDetailPage } from "./CustomerAdvanceDetailPage";
import { CustomerAdvanceListPage } from "./CustomerAdvanceListPage";

const ALL = ["create", "apply", "reverse", "refund"].map(
  (a) => `finance.customer-advances.${a}`,
);
const grant = (...keys: string[]) => keys.forEach((k) => granted.add(k));
/** Permisos de las API de los selectores. */
const LOOKUPS = [
  "masterdata.businesspartners.view",
  "treasury.banks.accounts.view",
  "caja.view",
  "sales.view",
];
const query = (name: string | RegExp) => screen.queryByRole("button", { name });

const movement = (
  id: string,
  movementType: string,
  extra: Record<string, unknown> = {},
) => ({
  id,
  movementType,
  amount: 10,
  lineNumber: 0,
  operationId: `op-${id}`,
  branchId: "br-1",
  effectiveDate: ADVANCE.advanceDate,
  evidenceDate: null,
  reason: null,
  reference: null,
  receivableId: null,
  installmentId: null,
  reversalOfMovementId: null,
  cashSessionId: null,
  cashMovementId: null,
  createdByUserId: "user-1",
  createdAtUtc: "2026-11-20T10:00:00Z",
  ...extra,
});

const detail = (
  overrides: Partial<CustomerAdvanceDto> = {},
): CustomerAdvanceDto => ({
  ...ADVANCE,
  movements: [movement("m1", "Received", { amount: 100 }) as never],
  ...overrides,
});

const at = (path: string) =>
  render(
    <I18nProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route
            path="/finance/customer-advances"
            element={<CustomerAdvanceListPage />}
          />
          <Route
            path="/finance/customer-advances/:id"
            element={<CustomerAdvanceDetailPage />}
          />
          <Route
            path="/finance/receivables"
            element={<AccountsReceivablePage />}
          />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );

beforeEach(() => {
  granted.clear();
  grant(...LOOKUPS);
  resetCatalogs();
  vi.mocked(message.confirm).mockResolvedValue(true);
  vi.mocked(receivableService.list).mockResolvedValue({
    items: [receivable()],
    total: 1,
    page: 1,
    pageSize: 100,
  });
});
afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("CustomerAdvanceListPage", () => {
  const page = (total: number) => ({
    items: [ADVANCE],
    total,
    page: 1,
    pageSize: 25,
  });

  it("pagina y filtra en el servidor mostrando saldo, sucursal y estado", async () => {
    vi.mocked(customerAdvanceService.list).mockResolvedValue(page(60));
    grant(...ALL);
    at("/finance/customer-advances");

    expect(await screen.findByText("Cliente Uno")).toBeTruthy();
    await waitFor(() =>
      expect(screen.getAllByText("Matriz").length).toBeGreaterThan(0),
    );
    expect(screen.getAllByText("Vigente").length).toBeGreaterThan(0);
    expect(customerAdvanceService.list).toHaveBeenLastCalledWith(1, 25, {});
    expect(query("Registrar anticipo")).toBeTruthy();

    fireEvent.click(screen.getByText("chevron_right").closest("button")!);
    await waitFor(() =>
      expect(customerAdvanceService.list).toHaveBeenLastCalledWith(2, 25, {}),
    );

    fireEvent.change(screen.getByLabelText("Estado"), {
      target: { value: "Cancelled" },
    });
    fireEvent.change(screen.getByLabelText("Sucursal de origen"), {
      target: { value: "br-1" },
    });
    fireEvent.click(screen.getByText("elegir cliente"));
    await waitFor(() =>
      expect(customerAdvanceService.list).toHaveBeenLastCalledWith(1, 25, {
        status: "Cancelled",
        branchId: "br-1",
        customerId: "cust-1",
      }),
    );
  });

  it("prioriza cliente, saldo, estado y detalle y distingue por medio y referencia", async () => {
    vi.mocked(customerAdvanceService.list).mockResolvedValue(page(1));
    at("/finance/customer-advances");
    await screen.findByText("Cliente Uno");

    const headers = screen
      .getAllByRole("columnheader")
      .map((h) => h.textContent);
    expect(headers.slice(1, 4)).toEqual([
      "Cliente",
      "Saldo disponible",
      "Estado",
    ]);
    const cells = within(screen.getAllByRole("row")[1]).getAllByRole("cell");
    await waitFor(() =>
      expect(cells[1].textContent).toContain("Transferencia"),
    );
    expect(cells[1].textContent).toContain("Referencia: REF-1");
    expect(within(cells[1]).getByRole("button", { name: "Ver" })).toBeTruthy();
  });

  it("muestra carga, vacío y error con reintento; sin permiso no ofrece registrar", async () => {
    vi.mocked(customerAdvanceService.list)
      .mockRejectedValueOnce(new Error("sin conexión"))
      .mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 25 });
    at("/finance/customer-advances");

    fireEvent.click(await screen.findByRole("button", { name: "Reintentar" }));
    expect(
      await screen.findByText("No hay anticipos para estos filtros."),
    ).toBeTruthy();
    expect(query("Registrar anticipo")).toBeNull();
  });
});

describe("CustomerAdvanceDetailPage", () => {
  const open = async (advance: CustomerAdvanceDto) => {
    vi.mocked(customerAdvanceService.getById).mockResolvedValue(advance);
    at("/finance/customer-advances/adv-1");
    await screen.findByText("Movimientos");
  };
  const actions = [
    "Aplicar anticipo",
    "Devolver dinero",
    "Revertir ingreso",
    "Anulación administrativa",
  ];

  it("con solo finance.view consulta el detalle y explica lo que no puede ver", async () => {
    granted.clear();
    grant("finance.view");
    await open(
      detail({
        movements: [
          movement("m2", "Application", {
            receivableId: "ar-1",
            lineNumber: 1,
          }) as never,
        ],
      }),
    );

    expect(screen.getByText(/Nombre no disponible sin permiso/)).toBeTruthy();
    expect(
      screen.getByText(/No tiene permiso para consultar cuentas por cobrar/),
    ).toBeTruthy();
    expect(screen.getByText("CxC no disponible")).toBeTruthy();
    expect(receivableService.list).not.toHaveBeenCalled();
  });

  it.each([
    ["Received", "+$ 10.00"],
    ["ReversalOfApplication", "+$ 10.00"],
    ["ReversalOfRefund", "+$ 10.00"],
    ["Application", "$ -10.00"],
    ["Refund", "$ -10.00"],
    ["IncomeReversed", "$ -10.00"],
    ["SourceCancelled", "$ -10.00"],
  ])(
    "el historial marca el efecto de %s sobre el saldo",
    async (type, effect) => {
      await open(detail({ movements: [movement("m1", type) as never] }));

      const cells = within(screen.getAllByRole("row")[1]).getAllByRole("cell");
      expect(cells[1].textContent?.replace(/\s/g, "")).toBe(
        effect.replace(/\s/g, ""),
      );
    },
  );

  it("sin permisos de acción solo consulta", async () => {
    await open(detail());
    actions.forEach((name) => expect(query(name)).toBeNull());
  });

  it.each([
    ["apply", ["Aplicar anticipo"]],
    ["refund", ["Devolver dinero", "Revertir ingreso"]],
    ["reverse", ["Anulación administrativa"]],
  ])(
    "el permiso %s habilita solo sus acciones",
    async (permission, expected) => {
      grant(`finance.customer-advances.${permission}`);
      await open(detail());
      actions.forEach((name) =>
        expect(!!query(name)).toBe(expected.includes(name)),
      );
    },
  );

  it("un anticipo consumido o en efectivo no ofrece anular ni revertir el ingreso", async () => {
    grant(...ALL);
    await open(detail({ availableAmount: 60 }));
    expect(query("Revertir ingreso")).toBeNull();
    expect(query("Anulación administrativa")).toBeNull();
    cleanup();

    await open(detail({ cashRegisterId: "reg-1", companyBankAccountId: null }));
    expect(query("Revertir ingreso")).toBeTruthy();
    expect(query("Anulación administrativa")).toBeNull();
  });

  it("muestra el historial completo, lo revertido y las reversas disponibles", async () => {
    grant(...ALL);
    const types = [
      "Application",
      "ReversalOfApplication",
      "Refund",
      "ReversalOfRefund",
      "IncomeReversed",
      "SourceCancelled",
    ];
    await open(
      detail({
        availableAmount: 70,
        movements: [
          movement("m1", "Received", { amount: 100 }),
          ...types.map((type, i) =>
            movement(`t${i}`, type, {
              receivableId: type.includes("Application") ? "ar-1" : null,
              lineNumber: type.includes("Application") ? 1 : 0,
              reason: `motivo ${type}`,
              reversalOfMovementId: type === "ReversalOfRefund" ? "t2" : null,
            }),
          ),
          movement("a2", "Application", {
            receivableId: "ar-1",
            lineNumber: 1,
          }),
        ] as never,
        refundTransactions: [
          {
            id: "tx-1",
            movementId: "t2",
            transactionType: "Refund",
            originalTransactionId: null,
            paymentMethodId: "pm-cash",
            companyBankAccountId: null,
            cashRegisterId: "reg-1",
            amount: 30,
            effectiveDate: ADVANCE.advanceDate,
            reference: "DEV-1",
            reason: "Devolución",
            cashSessionId: null,
            cashMovementId: null,
            createdByUserId: "user-1",
            createdAtUtc: "2026-11-20T10:00:00Z",
            isReversed: false,
          },
        ],
      }),
    );

    for (const label of [
      "Recepción",
      "Aplicación a CxC",
      "Reversión de aplicación",
      "Devolución",
      "Reversión de devolución",
      "Reversión del ingreso",
      "Anulación administrativa",
    ])
      expect(screen.getAllByText(label).length).toBeGreaterThan(0);
    expect(await screen.findAllByText("F-1")).not.toHaveLength(0);
    expect(screen.getByText(/Revierte: Devolución/)).toBeTruthy();
    expect(
      screen.getAllByRole("button", { name: "Revertir aplicación" }),
    ).toHaveLength(2);

    fireEvent.click(
      screen.getByRole("button", { name: "Revertir devolución" }),
    );
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText("Caja: Caja 1")).toBeTruthy();
  });

  it("tras una operación muestra el saldo oficial devuelto por el servidor", async () => {
    grant("finance.customer-advances.refund");
    vi.mocked(customerAdvanceService.refund).mockResolvedValue(
      detail({ availableAmount: 0, originalAmount: 100 }),
    );
    await open(detail());
    fireEvent.click(screen.getByRole("button", { name: "Devolver dinero" }));
    await screen.findByRole("option", { name: "Efectivo" });
    const dialog = screen.getByRole("dialog");
    fireEvent.change(within(dialog).getByLabelText(/^Medio de pago/), {
      target: { value: "pm-transfer" },
    });
    fireEvent.change(within(dialog).getByLabelText(/^Cuenta bancaria/), {
      target: { value: "bank:bank-1" },
    });
    fireEvent.change(
      within(dialog).getByLabelText(/^Referencia de ejecución/),
      {
        target: { value: "TRX" },
      },
    );
    fireEvent.change(within(dialog).getByLabelText(/^Motivo/), {
      target: { value: "Motivo" },
    });
    fireEvent.click(
      within(dialog).getByRole("button", { name: "Registrar devolución" }),
    );

    await waitFor(() => expect(query("Devolver dinero")).toBeNull());
    expect(query("Revertir ingreso")).toBeNull();
  });

  it("muestra el error de consulta con reintento", async () => {
    vi.mocked(customerAdvanceService.getById)
      .mockRejectedValueOnce(new Error("sin conexión"))
      .mockResolvedValue(detail());
    at("/finance/customer-advances/adv-1");
    fireEvent.click(await screen.findByRole("button", { name: "Reintentar" }));
    expect(await screen.findByText("Movimientos")).toBeTruthy();
  });
});

describe("AccountsReceivablePage — anticipos", () => {
  beforeEach(() => {
    vi.mocked(customerAdvanceService.balance).mockResolvedValue({
      customerId: "cust-1",
      currencyCode: "USD",
      availableAmount: 40,
    });
    vi.mocked(customerAdvanceService.byReceivable).mockResolvedValue([
      {
        customerAdvanceId: "adv-1",
        operationId: "op-1",
        movementId: "mv-1",
        installmentId: null,
        amount: 25,
        effectiveDate: ADVANCE.advanceDate,
        branchId: "br-1",
        createdByUserId: "user-1",
        createdAtUtc: "2026-11-20T10:00:00Z",
        isReversed: false,
        reversalMovementId: null,
        reversalDate: null,
      },
    ]);
    vi.mocked(customerAdvanceService.list).mockResolvedValue({
      items: [ADVANCE],
      total: 1,
      page: 1,
      pageSize: 100,
    });
  });

  const openRowActions = async () =>
    fireEvent.click(
      await screen.findByRole("button", { name: /^Acciones de factura/ }),
    );

  it("sin finance.view no ofrece anticipos y conserva el cobro", async () => {
    at("/finance/receivables");
    await openRowActions();
    expect(
      screen.getByRole("menuitem", { name: "Registrar cobro" }),
    ).toBeTruthy();
    expect(screen.queryByRole("menuitem", { name: "Anticipos" })).toBeNull();
  });

  it.each([
    [["finance.view"], false],
    [["finance.view", "finance.customer-advances.apply"], true],
  ])(
    "muestra saldo y aplicaciones; con %s aplicar = %s",
    async (permissions, canApply) => {
      grant(...permissions);
      at("/finance/receivables");
      await openRowActions();
      fireEvent.click(screen.getByRole("menuitem", { name: "Anticipos" }));

      expect(await screen.findByText("Ver anticipo")).toBeTruthy();
      expect(customerAdvanceService.balance).toHaveBeenCalledWith("cust-1");
      expect(customerAdvanceService.byReceivable).toHaveBeenCalledWith("ar-1");
      expect(!!query("Aplicar anticipo")).toBe(canApply);
    },
  );
});

describe("Acceso por URL y traducciones", () => {
  const menu = (routes: string[]) =>
    [{ items: routes.map((to) => ({ to })) }] as unknown as MainMenuGroup[];

  it("la lista y el detalle solo se montan si el menú del usuario incluye la pantalla", () => {
    for (const path of [
      "/finance/customer-advances",
      "/finance/customer-advances/adv-1",
    ]) {
      expect(isRouteAllowed(path, menu(["/finance/receivables"]))).toBe(false);
      expect(isRouteAllowed(path, menu(["/finance/customer-advances"]))).toBe(
        true,
      );
    }
  });

  it("es y en definen las mismas claves de anticipos, sin textos vacíos", () => {
    const keys = (dictionary: Record<string, string>) =>
      Object.keys(dictionary)
        .filter((k) => k.includes("customerAdvances"))
        .sort();
    expect(keys(en)).toEqual(keys(es));
    expect(keys(es).length).toBeGreaterThan(100);
    for (const key of keys(es)) {
      expect((es as Record<string, string>)[key].trim()).not.toBe("");
      expect((en as Record<string, string>)[key].trim()).not.toBe("");
    }
  });
});
