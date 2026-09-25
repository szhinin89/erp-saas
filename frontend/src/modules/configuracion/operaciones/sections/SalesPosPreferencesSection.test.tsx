// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render } from "@testing-library/react";
import { useForm } from "react-hook-form";
import { I18nProvider } from "../../../../i18n/i18n";
import { SalesPosPreferencesSection } from "./SalesPosPreferencesSection";
import { setPrecisionPolicyForTests } from "../../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../../test/precisionPolicyFixture";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-04D — "Descuento máximo" (POS) declara `precision="percentage"`: es el
 * tope operativo contra el que SalesDiscountUseCases compara el discountPct de la línea (que ya usa
 * percentage). Se guarda en OrgSettings (decimal sin escala fija; validador 0–100). El hook de la
 * sección se sustituye por un RHF real con el mismo shape; la lógica de guardado queda fuera.
 */

const values: { maxDiscountPercent?: unknown } = {};

vi.mock("./useSalesPosPreferencesSection", () => ({
  useSalesPosPreferencesSection: () => {
    const form = useForm({ defaultValues: { maxDiscountPercent: 10, allowManualDiscount: true, allowSellWithoutStock: false } });
    values.maxDiscountPercent = form.watch("maxDiscountPercent");
    return {
      canView: true,
      canEdit: true,
      saving: false,
      saved: false,
      saveError: null,
      isDirty: false,
      settingsState: { loading: false, error: null },
      form,
      errors: {},
      setValue: form.setValue,
      allowManualDiscountValue: true,
      allowSellWithoutStockValue: false,
      onSubmit: (e: Event) => e.preventDefault(),
      handleDiscard: () => {},
    };
  },
}));

afterEach(() => cleanup());

function renderSection() {
  const { container } = render(
    <I18nProvider>
      <SalesPosPreferencesSection />
    </I18nProvider>,
  );
  return container.querySelector<HTMLInputElement>('input[name="maxDiscountPercent"]')!;
}

function allowsDecimal(input: HTMLInputElement, digits: number) {
  fireEvent.change(input, { target: { value: `1.${"1".repeat(digits)}` } });
  input.setSelectionRange(input.value.length, input.value.length);
  return fireEvent.keyDown(input, { key: "9" });
}

describe("SalesPosPreferencesSection — precision='percentage' (04D)", () => {
  it("usa percentageDecimals de la policy (4) en vez del literal 2, y reacciona A → B sin remount", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, percentageDecimals: 4 });
    const input = renderSection();
    expect([allowsDecimal(input, 3), allowsDecimal(input, 4)]).toEqual([true, false]);
    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, percentageDecimals: 2 }));
    expect(allowsDecimal(input, 2)).toBe(false);
  });

  it("paste '12,5' → el formulario recibe el texto canónico '12.5' (mismo payload)", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, percentageDecimals: 2 });
    const input = renderSection();
    fireEvent.paste(input, { clipboardData: { getData: () => "12,5" } });
    expect(input.value).toBe("12.5");
    expect(values.maxDiscountPercent).toBe("12.5");
  });
});
