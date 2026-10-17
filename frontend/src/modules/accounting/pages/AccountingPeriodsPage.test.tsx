// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { AccountingPeriodsPage } from "./AccountingPeriodsPage";
import type {
  AccountingPeriodClosureReadinessDto,
  AccountingPeriodDto,
} from "../api/accountingApi";

/**
 * ACCOUNTING-07 — "Cerrar período" solo se habilita cuando el backend reporta el período terminado
 * (fecha de la empresa) y completo (`canClose`); incompleto o en curso → deshabilitado.
 */

const mocks = vi.hoisted(() => ({
  listPeriods: vi.fn(),
  getPeriodClosureReadiness: vi.fn(),
  denied: new Set<string>(),
}));
vi.mock("../api/accountingApi", () => ({
  accountingApi: {
    listPeriods: mocks.listPeriods,
    getPeriodClosureReadiness: mocks.getPeriodClosureReadiness,
  },
}));
vi.mock("../../../lib/messages", () => ({
  message: { error: vi.fn(), success: vi.fn(), warning: vi.fn(), confirm: vi.fn() },
}));
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => {
    const canShow = (p: string) => !mocks.denied.has(p);
    return { canShow, has: canShow, isAdminRole: false };
  },
}));

const period: AccountingPeriodDto = {
  id: "p-1",
  fiscalYear: 2026,
  periodNumber: 9,
  startDate: "2026-09-01",
  endDate: "2026-09-30",
  status: "Open",
  closedAtUtc: null,
  closedBy: null,
  closedByName: null,
  lockedAtUtc: null,
  lockedBy: null,
  lockedByName: null,
  createdAt: "2026-09-01T05:00:00Z",
  updatedAt: null,
};

const readiness = (
  hasEnded: boolean,
  isComplete: boolean,
): AccountingPeriodClosureReadinessDto => ({
  periodId: "p-1",
  status: "Open",
  isEligible: true,
  hasEnded,
  companyToday: hasEnded ? "2026-10-17" : "2026-09-20",
  canClose: hasEnded && isComplete,
  completeness: {
    isComplete,
    asOf: "2026-09-30",
    reasons: isComplete ? [] : [{ code: "POSTINGS_UNRESOLVED", count: 2 }],
  },
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  mocks.denied.clear();
});

it("A07: Cerrar período deshabilitado si el período está incompleto o no terminó", async () => {
  const cases: Array<[boolean, boolean, boolean]> = [
    [true, false, true], // terminado pero incompleto
    [false, true, true], // completo pero en curso
    [true, true, false], // terminado y completo → habilitado
  ];
  for (const [hasEnded, isComplete, disabled] of cases) {
    mocks.listPeriods.mockResolvedValue([period]);
    mocks.getPeriodClosureReadiness.mockResolvedValue(readiness(hasEnded, isComplete));
    render(
      <I18nProvider>
        <MemoryRouter>
          <AccountingPeriodsPage />
        </MemoryRouter>
      </I18nProvider>,
    );

    fireEvent.click(await screen.findByRole("button", { name: "Revisar cierre" }));
    await screen.findByText(/Evaluado al/);
    const close = screen.getByRole("button", { name: "Cerrar período" });
    expect((close as HTMLButtonElement).disabled).toBe(disabled);
    cleanup();
  }
});

it("A12B: SALE_COST_PENDING enlaza a /inventory/pending-costs solo con inventory.pending-costs.view", async () => {
  mocks.listPeriods.mockResolvedValue([period]);
  mocks.getPeriodClosureReadiness.mockResolvedValue({
    ...readiness(true, false),
    completeness: { isComplete: false, asOf: "2026-09-30", reasons: [{ code: "SALE_COST_PENDING", count: 1 }] },
  });
  const reasonItem = async () => {
    render(
      <I18nProvider>
        <MemoryRouter>
          <Routes>
            <Route path="/" element={<AccountingPeriodsPage />} />
            <Route path="/inventory/pending-costs" element={<p>pending-costs-route</p>} />
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );
    fireEvent.click(await screen.findByRole("button", { name: "Revisar cierre" }));
    return (await screen.findByText("Ventas con costo pendiente")).closest("li")!;
  };

  mocks.denied.add("inventory.pending-costs.view");
  const withoutPermission = await reasonItem();
  expect(withoutPermission.textContent).toContain("1");
  expect(within(withoutPermission).queryByRole("link")).toBeNull();
  cleanup();
  mocks.denied.clear();

  const link = within(await reasonItem()).getByRole("link");
  expect(link.getAttribute("href")).toBe("/inventory/pending-costs");
  fireEvent.click(link);
  expect(await screen.findByText("pending-costs-route")).toBeTruthy();
});
