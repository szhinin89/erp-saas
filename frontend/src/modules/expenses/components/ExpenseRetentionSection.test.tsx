// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useState } from "react";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
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
  candidates: [{ taxType: "IVA", retentionCode: "303", retentionCodeName: "Ret. IVA", retentionPct: 30 }],
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

/**
 * ZH-DESIGN-SYSTEM-PRECISION-04C — Base y Valor retenido → `precision="money"` (dominio los redondea
 * a FiscalPrecision.TaxAmount, misma fuente que moneyDecimals). "% Retención" BLOQUEADO: su escala
 * real es fiscal fija (FiscalPrecision.Percentage = 2, Math.Round en RetentionDocumentLine, XML
 * porcentajeRetener F2), no la percentageDecimals operativa, y ese SSOT no está expuesto al frontend.
 */
describe("ExpenseRetentionSection — precisión semántica (04C)", () => {
  function Stateful({ spy }: { spy: (patch: unknown) => void }) {
    const [value, setValue] = useState({
      ...emptyRetentionIntentState(),
      appliesRetention: true,
      emissionPointId: "ep-1",
      issueDate: "2026-09-01",
      lines: [newRetentionIntentLine()],
    });
    return (
      <ExpenseRetentionSection
        expenseDocumentId="exp-1"
        documentStatus="Draft"
        refreshKey={0}
        value={value}
        onChange={(patch) => {
          spy(patch);
          setValue((prev) => ({ ...prev, ...patch }));
        }}
        onEligibilityChange={() => {}}
      />
    );
  }

  function allowsDecimal(input: HTMLInputElement, digits: number) {
    fireEvent.change(input, { target: { value: `1.${"1".repeat(digits)}` } });
    input.setSelectionRange(input.value.length, input.value.length);
    return fireEvent.keyDown(input, { key: "9" });
  }

  it("Base y Valor retenido usan moneyDecimals de la policy (sintética 3)", async () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 });
    render(<Stateful spy={() => {}} />);
    await waitFor(() => expect(screen.getByLabelText(/^Base/)).toBeTruthy());
    const base = screen.getByLabelText(/^Base/) as HTMLInputElement;
    const retained = screen.getByLabelText(/^Valor retenido/) as HTMLInputElement;
    expect([allowsDecimal(base, 2), allowsDecimal(base, 3)]).toEqual([true, false]);
    expect([allowsDecimal(retained, 2), allowsDecimal(retained, 3)]).toEqual([true, false]);
  });

  it("paste '1.234,56' en Base → onChange con baseAmount '1234.56' (mismo payload string)", async () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    const spy = vi.fn();
    render(<Stateful spy={spy} />);
    await waitFor(() => expect(screen.getByLabelText(/^Base/)).toBeTruthy());
    fireEvent.paste(screen.getByLabelText(/^Base/), { clipboardData: { getData: () => "1.234,56" } });
    const patch = spy.mock.calls.at(-1)![0] as { lines: { baseAmount: string }[] };
    expect(patch.lines[0]!.baseAmount).toBe("1234.56");
  });

  // 04C1: antes "BLOQUEADO — sigue en percentageDecimals (policy 4 → admitía 4 decimales)".
  it("'% Retención' usa fiscalPercentage: con percentageDecimals=4 y fiscalPercentageDecimals=2 admite SOLO 2", async () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, percentageDecimals: 4, fiscalPercentageDecimals: 2 });
    render(<Stateful spy={() => {}} />);
    await waitFor(() => expect(screen.getByLabelText(/^% Retención/)).toBeTruthy());
    const rate = screen.getByLabelText(/^% Retención/) as HTMLInputElement;
    expect([allowsDecimal(rate, 1), allowsDecimal(rate, 2)]).toEqual([true, false]);
  });
});
