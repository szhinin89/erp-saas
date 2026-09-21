// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, waitFor, fireEvent, within } from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";
import { dictionaries } from "../../../../i18n/dictionaries";
import { precisionExample, PRECISION_SECTIONS } from "../precisionPolicyFields";
import type { PrecisionPolicy } from "../../../../lib/config/precisionPolicy.config";

// ERP-PRECISION-POLICY-SETTINGS-UX-02 — estructura, ejemplos dinámicos y bloqueo de la pantalla
// de precisión decimal por empresa. No cambia rangos, defaults ni cálculos.

const loadMock = vi.fn();
const saveMock = vi.fn();
vi.mock("../../../../lib/config/precisionPolicy.config", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../lib/config/precisionPolicy.config")>();
  return {
    ...actual,
    loadPrecisionPolicy: () => loadMock(),
    savePrecisionPolicy: (v: unknown) => saveMock(v),
  };
});

let canEdit = true;
vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({
    canShow: (perm: string) => (perm === "erp.companies.update" ? canEdit : true),
  }),
}));

import { PrecisionPolicySettingsSection } from "./PrecisionPolicySettingsSection";

function policy(overrides: Partial<PrecisionPolicy> = {}): PrecisionPolicy {
  return {
    profileType: "Custom",
    salesUnitPriceDecimals: 2,
    purchaseUnitPriceDecimals: 4,
    quantityDecimals: 4,
    percentageDecimals: 2,
    unitCostDecimals: 6,
    averageCostDecimals: 6,
    conversionFactorDecimals: 6,
    settlementToleranceAmount: 0.01,
    isLocked: false,
    lockedAt: null,
    lockedReason: null,
    moneyDecimals: 2,
    taxDecimals: 2,
    accountingDecimals: 2,
    ...overrides,
  } as PrecisionPolicy;
}

async function renderSection(p: PrecisionPolicy) {
  loadMock.mockResolvedValue(p);
  const utils = render(
    <I18nProvider>
      <PrecisionPolicySettingsSection />
    </I18nProvider>,
  );
  await waitFor(() => expect(screen.queryByTestId("precision-section-prices")).not.toBeNull());
  return utils;
}

const input = (name: string) =>
  document.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;

beforeEach(() => {
  canEdit = true;
  loadMock.mockReset();
  saveMock.mockReset();
  localStorage.clear();
});
afterEach(() => cleanup());

describe("precisionExample — N decimales exactos (unitarios)", () => {
  it("precio con 2 / 4 / 8 decimales", () => {
    expect(precisionExample("price", 2)).toBe("$0.12");
    expect(precisionExample("price", 4)).toBe("$0.1234");
    expect(precisionExample("price", 8)).toBe("$0.12345678");
  });
  it("cantidad, factor, porcentaje y 0 decimales", () => {
    expect(precisionExample("quantity", 3)).toBe("12.123");
    expect(precisionExample("factor", 6)).toBe("1.123456");
    expect(precisionExample("percentage", 2)).toBe("15.12%");
    expect(precisionExample("quantity", 0)).toBe("12");
  });
});

describe("estructura: secciones y campos (keys existentes)", () => {
  it("las cuatro secciones con sus campos, sin claves nuevas", () => {
    expect(PRECISION_SECTIONS.map((s) => [s.id, s.fields.map((f) => f.name)])).toEqual([
      ["prices", ["salesUnitPriceDecimals", "purchaseUnitPriceDecimals"]],
      ["costs", ["unitCostDecimals", "averageCostDecimals"]],
      ["quantities", ["quantityDecimals", "conversionFactorDecimals"]],
      ["other", ["percentageDecimals"]],
    ]);
  });

  it("muestra secciones, labels humanos y el bloque de precisión fiscal", async () => {
    await renderSection(policy());
    for (const [id, title] of [
      ["prices", "Precios"],
      ["costs", "Costos"],
      ["quantities", "Cantidades y conversiones"],
      ["other", "Otros"],
    ]) {
      expect(within(screen.getByTestId(`precision-section-${id}`)).getByText(title)).toBeTruthy();
    }
    for (const label of [
      "Precio unitario de venta",
      "Precio unitario de compra",
      "Costo unitario",
      "Costo promedio de inventario",
      "Cantidad",
      "Factor de conversión",
      "Porcentajes",
    ]) {
      expect(screen.getByText(label)).toBeTruthy();
    }
    // Aclara que son valores UNITARIOS
    expect(
      screen.getByText(/Decimales del precio de UN producto al vender \(valor unitario/),
    ).toBeTruthy();
    const fiscal = screen.getByTestId("precision-fiscal-block").textContent ?? "";
    expect(fiscal).toContain("Precisión fiscal");
    expect(fiscal).toContain("no se configuran aquí");
  });

  it("los campos se muestran también con perfiles predefinidos (deshabilitados)", async () => {
    await renderSection(policy({ profileType: "StandardCommercial" }));
    await waitFor(() =>
      expect(screen.getByTestId("precision-custom-hint").textContent).toContain("Personalizado"),
    );
    expect(input("salesUnitPriceDecimals").value).toBe("2");
    expect(input("salesUnitPriceDecimals").disabled).toBe(true);
  });
});

describe("ejemplos dinámicos según N decimales", () => {
  it("reflejan el valor actual y se actualizan al editar", async () => {
    await renderSection(policy({ salesUnitPriceDecimals: 2, purchaseUnitPriceDecimals: 4 }));
    expect(screen.getByTestId("precision-example-salesUnitPriceDecimals").textContent).toContain("$0.12");
    expect(screen.getByTestId("precision-example-purchaseUnitPriceDecimals").textContent).toContain("$0.1234");
    expect(screen.getByTestId("precision-example-quantityDecimals").textContent).toContain("12.1234");
    expect(screen.getByTestId("precision-example-percentageDecimals").textContent).toContain("15.12%");

    fireEvent.change(input("salesUnitPriceDecimals"), { target: { value: "6" } });
    await waitFor(() =>
      expect(screen.getByTestId("precision-example-salesUnitPriceDecimals").textContent).toContain(
        "$0.123456",
      ),
    );
  });
});

describe("desbloqueado: permite edición y guardado", () => {
  it("con perfil Personalizado los campos son editables y Guardar envía los valores", async () => {
    saveMock.mockResolvedValue(policy({ salesUnitPriceDecimals: 4 }));
    await renderSection(policy());
    await waitFor(() => expect(input("salesUnitPriceDecimals").disabled).toBe(false));
    const sales = input("salesUnitPriceDecimals");
    expect(screen.queryByTestId("precision-custom-hint")).toBeNull();

    fireEvent.change(sales, { target: { value: "4" } });
    fireEvent.click(screen.getByRole("button", { name: /Guardar Configuración/ }));

    await waitFor(() => expect(saveMock).toHaveBeenCalledTimes(1));
    expect(saveMock.mock.calls[0][0]).toMatchObject({
      profileType: "Custom",
      salesUnitPriceDecimals: 4,
      purchaseUnitPriceDecimals: 4,
      unitCostDecimals: 6,
    });
  });

  it("perfil predefinido: aunque los campos estén deshabilitados, Guardar envía todos los valores del perfil", async () => {
    saveMock.mockResolvedValue(policy({ profileType: "HighPrecision" }));
    await renderSection(policy());
    fireEvent.click(screen.getByRole("radio", { name: /Alta precisión/ }));
    fireEvent.click(screen.getByRole("button", { name: /Guardar Configuración/ }));
    await waitFor(() => expect(saveMock).toHaveBeenCalledTimes(1));
    expect(saveMock.mock.calls[0][0]).toMatchObject({
      profileType: "HighPrecision",
      salesUnitPriceDecimals: 4,
      purchaseUnitPriceDecimals: 6,
      quantityDecimals: 6,
      percentageDecimals: 4,
      conversionFactorDecimals: 8,
    });
  });

  it("sin permiso de edición, todo queda deshabilitado", async () => {
    canEdit = false;
    await renderSection(policy());
    expect(input("salesUnitPriceDecimals").disabled).toBe(true);
    expect(screen.getByRole("button", { name: /Guardar Configuración/ }).hasAttribute("disabled")).toBe(true);
  });
});

describe("bloqueado por operaciones", () => {
  const locked = () => policy({ isLocked: true, lockedReason: "Documentos autorizados" });

  it("muestra los valores pero ningún campo es editable", async () => {
    await renderSection(locked());
    for (const name of [
      "salesUnitPriceDecimals",
      "purchaseUnitPriceDecimals",
      "unitCostDecimals",
      "averageCostDecimals",
      "quantityDecimals",
      "conversionFactorDecimals",
      "percentageDecimals",
      "settlementToleranceAmount",
    ]) {
      expect(input(name).disabled, name).toBe(true);
    }
    expect(input("salesUnitPriceDecimals").value).toBe("2");
    expect(input("unitCostDecimals").value).toBe("6");
    // los ejemplos siguen visibles
    expect(screen.getByTestId("precision-example-unitCostDecimals").textContent).toContain("$0.123456");
  });

  it("muestra el mensaje de bloqueo (y el motivo, si existe)", async () => {
    await renderSection(locked());
    expect(
      screen.getByText(
        "Esta empresa ya registra operaciones. La precisión decimal está bloqueada para proteger la consistencia histórica.",
      ),
    ).toBeTruthy();
    expect(screen.getByText("Documentos autorizados")).toBeTruthy();
  });

  it("oculta Guardar y los perfiles quedan deshabilitados", async () => {
    await renderSection(locked());
    expect(screen.queryByRole("button", { name: /Guardar Configuración/ })).toBeNull();
    expect(screen.queryByRole("button", { name: /Descartar/ })).toBeNull();
    for (const radio of screen.getAllByRole("radio")) {
      expect(radio.hasAttribute("disabled")).toBe(true);
    }
    expect(screen.queryByTestId("precision-custom-hint")).toBeNull();
  });

  it("no guarda aunque se fuerce el submit del formulario", async () => {
    await renderSection(locked());
    const form = document.querySelector("form")!;
    fireEvent.submit(form);
    await new Promise((r) => setTimeout(r, 20));
    expect(saveMock).not.toHaveBeenCalled();
  });

  it("no cambia valores al intentar seleccionar un perfil", async () => {
    await renderSection(locked());
    fireEvent.click(screen.getByRole("radio", { name: /Alta precisión/ }));
    expect(input("salesUnitPriceDecimals").value).toBe("2");
  });
});

describe("i18n ES/EN/QU", () => {
  const keys = [
    ...["prices", "costs", "quantities", "other"].map((s) => `settings.company.precision.section.${s}`),
    ...["salesUnitPrice", "purchaseUnitPrice", "unitCost", "averageCost", "quantity", "conversionFactor", "percentage", "tolerance"].flatMap(
      (f) => [`settings.company.precision.field.${f}.label`, `settings.company.precision.field.${f}.desc`],
    ),
    "settings.company.precision.example",
    "settings.company.precision.customHint",
    "settings.company.precision.fiscal.title",
    "settings.company.precision.fiscal.body",
    "settings.company.precision.locked",
  ];
  it.each(["es", "en", "qu"] as const)("locale %s tiene todas las claves", (locale) => {
    for (const key of keys) expect(dictionaries[locale][key], `${locale}:${key}`).toBeTruthy();
    expect(dictionaries[locale]["settings.company.precision.example"]).toContain("{{value}}");
  });
});
