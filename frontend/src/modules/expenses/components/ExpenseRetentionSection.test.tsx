// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ExpenseRetentionSection } from "./ExpenseRetentionSection";
import { emissionPointsService } from "../../emissionPoints/api/emissionPointsService";
import { expenseDocumentService } from "../api/expenseDocumentService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { emptyRetentionIntentState, newRetentionIntentLine } from "../utils/expenseRetentionModel";
import type { RetentionEligibilityResult } from "../api/expenseDocumentService";

/**
 * RETENTIONS-UI-REMOVE-MANUAL-NUMBER-02F — pruebas de componente aisladas para
 * `ExpenseRetentionSection`, complementarias a la cobertura de integración ya existente en
 * `ExpenseDocumentFormPage.retention.test.tsx`. Foco específico de esta suite: (1) ya no existe
 * ningún input editable de número de retención, (2) se muestra el mensaje de generación
 * automática, y (3) el resto de la sección (checkbox, punto de emisión, líneas) sigue
 * funcionando sin cambios.
 */

vi.mock("../../emissionPoints/api/emissionPointsService", () => ({
  emissionPointsService: { list: vi.fn() },
}));

vi.mock("../api/expenseDocumentService", () => ({
  expenseDocumentService: {
    getRetentionEligibility: vi.fn(),
    getExpenseRetention: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

const EMISSION_POINT = {
  id: "ep-1",
  establishmentId: "est-1",
  establishmentCode: "001",
  establishmentName: "Matriz",
  branchName: null,
  code: "001",
  name: "Punto principal",
  emissionType: "Physical" as const,
  isDefault: true,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
};

const ELIGIBLE_RESULT: RetentionEligibilityResult = {
  canRetainVat: true,
  canRetainIncome: false,
  isSupplierExempt: false,
  hasRetainableBase: true,
  missingRetentionCode: false,
  isSupplierRequiredToKeepAccounting: false,
  suggestedVatRetentionCode: "303",
  suggestedIncomeRetentionCode: null,
  reasons: ["La empresa actual está configurada para retener IVA."],
  isEligible: true,
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
  vi.mocked(emissionPointsService.list).mockResolvedValue([EMISSION_POINT]);
  vi.mocked(expenseDocumentService.getRetentionEligibility).mockResolvedValue(ELIGIBLE_RESULT);
  vi.mocked(expenseDocumentService.getExpenseRetention).mockResolvedValue(null);
});

afterEach(() => {
  cleanup();
});

function renderSection(overrides: Partial<Parameters<typeof ExpenseRetentionSection>[0]> = {}) {
  const onChange = vi.fn();
  const onEligibilityChange = vi.fn();
  const value = overrides.value ?? {
    ...emptyRetentionIntentState(),
    appliesRetention: true,
    emissionPointId: "ep-1",
    issueDate: "2026-09-01",
    lines: [newRetentionIntentLine()],
  };

  render(
    <ExpenseRetentionSection
      expenseDocumentId="exp-1"
      documentStatus="Draft"
      refreshKey={0}
      value={value}
      onChange={onChange}
      onEligibilityChange={onEligibilityChange}
      {...overrides}
    />,
  );

  return { onChange, onEligibilityChange, value };
}

describe("ExpenseRetentionSection — sin número de retención manual", () => {
  it("no renderiza ningún input ni label de número de retención", async () => {
    renderSection();

    await waitFor(() => expect(screen.getByText("Punto de emisión")).toBeTruthy());

    expect(screen.queryByText("Número de retención")).toBeNull();
    expect(screen.queryByLabelText(/número de retención/i)).toBeNull();
    expect(screen.queryByText(/número de retención/i, { selector: "label" })).toBeNull();
  });

  it("muestra el mensaje de generación automática del número", async () => {
    renderSection();

    await waitFor(() =>
      expect(
        screen.getByText(
          "El número de retención se generará automáticamente al confirmar este documento.",
        ),
      ).toBeTruthy(),
    );
  });

  it("mantiene visible el selector de punto de emisión", async () => {
    renderSection();

    await waitFor(() => expect(screen.getByText("Punto de emisión")).toBeTruthy());
    expect(screen.getByLabelText(/^Punto de emisión/)).toBeTruthy();
  });

  it("regresión: el checkbox de aplicar retención sigue notificando appliesRetention vía onChange", async () => {
    const { onChange } = renderSection({
      value: { ...emptyRetentionIntentState(), emissionPointId: "", issueDate: "" },
    });

    await waitFor(() => {
      const toggle = screen.getByLabelText("Aplicar retención a este gasto") as HTMLInputElement;
      expect(toggle.disabled).toBe(false);
    });

    fireEvent.click(screen.getByLabelText("Aplicar retención a este gasto"));

    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ appliesRetention: true }),
    );
  });

  it("regresión: código de retención, base, porcentaje y monto retenido siguen editables", async () => {
    const { onChange, value } = renderSection();
    const line = value.lines[0];

    await waitFor(() => expect(screen.getByLabelText(/^Código de retención/)).toBeTruthy());

    fireEvent.change(screen.getByLabelText(/^Código de retención/), {
      target: { value: "303" },
    });
    expect(onChange).toHaveBeenCalledWith({
      lines: [{ ...line, retentionCode: "303" }],
    });

    fireEvent.change(screen.getByLabelText(/^Base/), { target: { value: "100" } });
    expect(onChange).toHaveBeenCalledWith({
      lines: [{ ...line, baseAmount: "100" }],
    });

    fireEvent.change(screen.getByLabelText(/^% Retención/), { target: { value: "30" } });
    expect(onChange).toHaveBeenCalledWith({
      lines: [{ ...line, retentionRate: "30" }],
    });

    fireEvent.change(screen.getByLabelText(/^Valor retenido/), { target: { value: "30" } });
    expect(onChange).toHaveBeenCalledWith({
      lines: [{ ...line, retainedAmount: "30" }],
    });
  });
});
