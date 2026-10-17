// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
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
  usePermissionsUi: () => ({ canShow: () => true, has: () => true, isAdminRole: false }),
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
