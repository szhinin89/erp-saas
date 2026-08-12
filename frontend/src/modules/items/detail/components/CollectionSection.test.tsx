// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { PackagingLevelsSection } from "./CollectionSection";
import type { ItemPackagingLevelDto } from "../../../../types/items";

afterEach(() => cleanup());

const t = (_key: string, fallback?: string) => fallback ?? _key;

const packagingLevels: ItemPackagingLevelDto[] = [
  {
    id: "paca-12",
    name: "PACA",
    level: 2,
    baseQuantity: 12,
    uomCode: "PACA",
    uomAbbrev: "PACA",
    barcode: null,
    weight: null,
    isBaseUnit: false,
    isPurchaseDefault: true,
    isSaleDefault: false,
    isActive: true,
  },
];

const uomOptions = [
  { code: "UNIDAD", name: "Unidad", abbrev: "UND" },
  { code: "PACA", name: "Paca", abbrev: "PACA" },
];

const baseUnitLevel: ItemPackagingLevelDto = {
  id: "unidad-1",
  name: "UNIDAD x1",
  level: 1,
  baseQuantity: 1,
  uomCode: "UNIDAD",
  uomAbbrev: "UND",
  barcode: null,
  weight: null,
  isBaseUnit: true,
  isPurchaseDefault: false,
  isSaleDefault: true,
  isActive: true,
};

describe("PackagingLevelsSection", () => {
  it("renderiza estado vacío con botón Agregar presentación", () => {
    render(
      <PackagingLevelsSection
        t={t}
        levels={[]}
        uomOptions={uomOptions}
        baseUomCode="UNIDAD"
        tracksStock
        usedPackagingLevelIds={new Set()}
        onSave={vi.fn()}
      />,
    );

    expect(
      screen.getByText(
        "No hay niveles de empaque configurados. Cree primero una presentación base UNIDAD X1 y luego agregue PACA x12 o CAJA x24.",
      ),
    ).toBeTruthy();
    expect(
      screen.getByRole("button", { name: "Agregar presentación" }),
    ).toBeTruthy();
    expect(screen.getByRole("button", { name: "Crear UNIDAD X1" })).toBeTruthy();
  });

  it("permite agregar PACA x12 y llama onSave con el conjunto completo", async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    render(
      <PackagingLevelsSection
        t={t}
        levels={[baseUnitLevel]}
        uomOptions={uomOptions}
        baseUomCode="UNIDAD"
        tracksStock
        usedPackagingLevelIds={new Set()}
        onSave={onSave}
      />,
    );

    fireEvent.click(
      screen.getByRole("button", { name: "Agregar presentación" }),
    );

    const nameInput = screen.getByPlaceholderText("PACA x12");
    fireEvent.change(nameInput, { target: { value: "PACA x12" } });

    const selects = screen.getAllByRole("combobox");
    fireEvent.change(selects[selects.length - 1], {
      target: { value: "PACA" },
    });

    const quantityInputs = document.querySelectorAll(
      ".zh-numeric-input",
    ) as NodeListOf<HTMLInputElement>;
    fireEvent.change(quantityInputs[quantityInputs.length - 2], {
      target: { value: "12" },
    });

    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await Promise.resolve();
    await Promise.resolve();

    expect(onSave).toHaveBeenCalledTimes(1);
    const payload = onSave.mock.calls[0][0];
    expect(payload).toHaveLength(2);
    expect(payload[0]).toMatchObject({
      name: "UNIDAD x1",
      uomCode: "UNIDAD",
      baseQuantity: 1,
    });
    expect(payload[1]).toMatchObject({
      id: null,
      name: "PACA x12",
      uomCode: "PACA",
      baseQuantity: 12,
    });
  });

  it("intentar guardar sin unidad base muestra error y no llama onSave", () => {
    const onSave = vi.fn();
    render(
      <PackagingLevelsSection
        t={t}
        levels={[]}
        uomOptions={uomOptions}
        baseUomCode="UNIDAD"
        tracksStock
        usedPackagingLevelIds={new Set()}
        onSave={onSave}
      />,
    );

    fireEvent.click(
      screen.getByRole("button", { name: "Agregar presentación" }),
    );
    fireEvent.change(screen.getByPlaceholderText("PACA x12"), {
      target: { value: "PACA x12" },
    });
    fireEvent.change(screen.getByRole("combobox"), {
      target: { value: "PACA" },
    });
    const quantityInputs = document.querySelectorAll(
      ".zh-numeric-input",
    ) as NodeListOf<HTMLInputElement>;
    fireEvent.change(quantityInputs[0], { target: { value: "12" } });

    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    expect(
      screen.getByText(
        "Debe existir una presentación base, por ejemplo UNIDAD X1 con cantidad base 1.",
      ),
    ).toBeTruthy();
    expect(onSave).not.toHaveBeenCalled();
  });

  it("crea fácilmente UNIDAD X1 desde el estado vacío", async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    render(
      <PackagingLevelsSection
        t={t}
        levels={[]}
        uomOptions={uomOptions}
        baseUomCode="UNIDAD"
        tracksStock
        usedPackagingLevelIds={new Set()}
        onSave={onSave}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Crear UNIDAD X1" }));
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await Promise.resolve();
    await Promise.resolve();

    expect(onSave).toHaveBeenCalledWith([
      expect.objectContaining({
        name: "UNIDAD X1",
        uomCode: "UNIDAD",
        baseQuantity: 1,
        isBaseUnit: true,
      }),
    ]);
  });

  it("conserva la fila en edición y muestra el error real si onSave falla", async () => {
    const onSave = vi
      .fn()
      .mockRejectedValue(
        new Error("Debe existir exactamente una presentación marcada como unidad base."),
      );
    render(
      <PackagingLevelsSection
        t={t}
        levels={[baseUnitLevel]}
        uomOptions={uomOptions}
        baseUomCode="UNIDAD"
        tracksStock
        usedPackagingLevelIds={new Set()}
        onSave={onSave}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Editar" }));
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() =>
      expect(
        screen.getByText(
          "Debe existir exactamente una presentación marcada como unidad base.",
        ),
      ).toBeTruthy(),
    );
    expect(screen.getByRole("button", { name: "Cancelar" })).toBeTruthy();
  });

  it("editar PACA de cantidad 1 a 12 envía el id existente y la cantidad nueva", async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    const pacaLevel: ItemPackagingLevelDto = {
      ...packagingLevels[0],
      baseQuantity: 1,
      isPurchaseDefault: true,
    };

    render(
      <PackagingLevelsSection
        t={t}
        levels={[baseUnitLevel, pacaLevel]}
        uomOptions={uomOptions}
        baseUomCode="UNIDAD"
        tracksStock
        usedPackagingLevelIds={new Set()}
        onSave={onSave}
      />,
    );

    const row = screen.getAllByText("PACA")[0].closest("tr")!;
    fireEvent.click(within(row).getByRole("button", { name: "Editar" }));
    const quantityInputs = document.querySelectorAll(
      ".zh-numeric-input",
    ) as NodeListOf<HTMLInputElement>;
    fireEvent.change(quantityInputs[0], { target: { value: "12" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await Promise.resolve();
    await Promise.resolve();

    expect(onSave).toHaveBeenCalledTimes(1);
    const payload = onSave.mock.calls[0][0];
    expect(payload).toContainEqual(
      expect.objectContaining({
        id: "paca-12",
        name: "PACA",
        baseQuantity: 12,
      }),
    );
  });

  it("PACA x12 con BaseQuantity 1 muestra advertencia visual sin inferir factor", () => {
    const pacaLevel: ItemPackagingLevelDto = {
      ...packagingLevels[0],
      name: "PACA x12",
      baseQuantity: 1,
    };

    render(
      <PackagingLevelsSection
        t={t}
        levels={[baseUnitLevel, pacaLevel]}
        uomOptions={uomOptions}
        baseUomCode="UNIDAD"
        tracksStock
        usedPackagingLevelIds={new Set()}
        onSave={vi.fn()}
      />,
    );

    expect(
      screen.getByText(
        "El nombre sugiere una presentación múltiple, pero la cantidad base es 1. Revise el factor; no se infiere automáticamente.",
      ),
    ).toBeTruthy();
  });

  it("valida que BaseQuantity sea mayor a 0", () => {
    render(
      <PackagingLevelsSection
        t={t}
        levels={[]}
        uomOptions={uomOptions}
        baseUomCode="UNIDAD"
        tracksStock
        usedPackagingLevelIds={new Set()}
        onSave={vi.fn()}
      />,
    );

    fireEvent.click(
      screen.getByRole("button", { name: "Agregar presentación" }),
    );
    fireEvent.change(screen.getByPlaceholderText("PACA x12"), {
      target: { value: "PACA x12" },
    });
    fireEvent.change(screen.getByRole("combobox"), {
      target: { value: "PACA" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    expect(
      screen.getByText("La cantidad base debe ser mayor a 0."),
    ).toBeTruthy();
  });

  it("si IsBaseUnit=true exige BaseQuantity=1", () => {
    render(
      <PackagingLevelsSection
        t={t}
        levels={[]}
        uomOptions={uomOptions}
        baseUomCode="UNIDAD"
        tracksStock
        usedPackagingLevelIds={new Set()}
        onSave={vi.fn()}
      />,
    );

    fireEvent.click(
      screen.getByRole("button", { name: "Agregar presentación" }),
    );
    fireEvent.change(screen.getByPlaceholderText("PACA x12"), {
      target: { value: "PACA x12" },
    });
    fireEvent.change(screen.getByRole("combobox"), {
      target: { value: "PACA" },
    });
    const quantityInputs = document.querySelectorAll(
      ".zh-numeric-input",
    ) as NodeListOf<HTMLInputElement>;
    fireEvent.change(quantityInputs[0], { target: { value: "12" } });
    const checkboxes = screen.getAllByRole("checkbox");
    fireEvent.click(checkboxes[0]);
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    expect(
      screen.getByText("La unidad base debe tener cantidad 1."),
    ).toBeTruthy();
  });

  it("no permite duplicar unidad y equivalencia en el mismo ítem", () => {
    render(
      <PackagingLevelsSection
        t={t}
        levels={[baseUnitLevel]}
        uomOptions={uomOptions}
        baseUomCode="UNIDAD"
        tracksStock
        usedPackagingLevelIds={new Set()}
        onSave={vi.fn()}
      />,
    );

    fireEvent.click(
      screen.getByRole("button", { name: "Agregar presentación" }),
    );
    fireEvent.change(screen.getByPlaceholderText("PACA x12"), {
      target: { value: "UNIDAD duplicada" },
    });
    const selects = screen.getAllByRole("combobox");
    fireEvent.change(selects[selects.length - 1], {
      target: { value: "UNIDAD" },
    });
    const quantityInputs = document.querySelectorAll(
      ".zh-numeric-input",
    ) as NodeListOf<HTMLInputElement>;
    fireEvent.change(quantityInputs[quantityInputs.length - 2], {
      target: { value: "1" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    expect(
      screen.getByText(
        "Ya existe una presentación con esa unidad y cantidad.",
      ),
    ).toBeTruthy();
  });

  it("no elimina un empaque en uso por un código de proveedor", () => {
    render(
      <PackagingLevelsSection
        t={t}
        levels={[baseUnitLevel]}
        uomOptions={uomOptions}
        baseUomCode="UNIDAD"
        tracksStock
        usedPackagingLevelIds={new Set(["unidad-1"])}
        onSave={vi.fn()}
      />,
    );

    const row = screen.getByText("UNIDAD x1").closest("tr")!;
    const removeBtn = within(row).getByRole("button", { name: "Quitar" });
    expect(removeBtn).toHaveProperty("disabled", true);
  });

  it("preserva niveles existentes al agregar uno nuevo", async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    const secondLevel: ItemPackagingLevelDto = {
      ...baseUnitLevel,
      id: "caja-1",
      name: "CAJA x24",
      level: 2,
      baseQuantity: 24,
      uomCode: "PACA",
      uomAbbrev: "PACA",
      isBaseUnit: false,
      isSaleDefault: false,
    };

    render(
      <PackagingLevelsSection
        t={t}
        levels={[baseUnitLevel, secondLevel]}
        uomOptions={[...uomOptions, { code: "CAJA", name: "Caja", abbrev: "CAJA" }]}
        baseUomCode="UNIDAD"
        tracksStock
        usedPackagingLevelIds={new Set()}
        onSave={onSave}
      />,
    );

    fireEvent.click(
      screen.getByRole("button", { name: "Agregar presentación" }),
    );
    fireEvent.change(screen.getByPlaceholderText("PACA x12"), {
      target: { value: "CAJA x48" },
    });
    const selects = screen.getAllByRole("combobox");
    fireEvent.change(selects[selects.length - 1], {
      target: { value: "CAJA" },
    });
    const quantityInputs = document.querySelectorAll(
      ".zh-numeric-input",
    ) as NodeListOf<HTMLInputElement>;
    fireEvent.change(quantityInputs[quantityInputs.length - 2], {
      target: { value: "48" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await Promise.resolve();
    await Promise.resolve();

    const payload = onSave.mock.calls[0][0];
    expect(payload).toHaveLength(3);
    expect(payload.map((p: { name: string }) => p.name)).toEqual([
      "UNIDAD x1",
      "CAJA x24",
      "CAJA x48",
    ]);
  });

  it("no tiene styles inline", () => {
    const { container } = render(
      <PackagingLevelsSection
        t={t}
        levels={[baseUnitLevel]}
        uomOptions={uomOptions}
        baseUomCode="UNIDAD"
        tracksStock
        usedPackagingLevelIds={new Set()}
        onSave={vi.fn()}
      />,
    );

    expect(container.querySelectorAll("[style]").length).toBe(0);
  });
});
