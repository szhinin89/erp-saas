// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { FormProvider, useForm } from "react-hook-form";
import { BarcodeListEditor } from "./BarcodeListEditor";
import { SupplierCodesSection } from "./SupplierCodesSection";
import {
  defaultCreateItemValues,
  type CreateItemFormValues,
} from "../../schemas/createItemSchema";

vi.mock("../../../purchases/components/SupplierPicker", () => ({
  SupplierPicker: () => <input aria-label="Proveedor" />,
}));

afterEach(() => cleanup());

const t = (_key: string, fallback?: string) => fallback ?? _key;

function CodesHarness() {
  const form = useForm<CreateItemFormValues>({
    defaultValues: defaultCreateItemValues as CreateItemFormValues,
  });

  return (
    <FormProvider {...form}>
      <BarcodeListEditor
        t={t}
        disabled={false}
        barcodeTypeOptions={[{ code: "EAN13", name: "EAN 13" }]}
      />
      <SupplierCodesSection t={t} disabled={false} />
    </FormProvider>
  );
}

describe("Item Principal create code sections", () => {
  it("muestra acciones para agregar códigos en creación", () => {
    render(<CodesHarness />);

    expect(
      screen.getByRole("button", { name: "Agregar código de barras" }),
    ).toBeTruthy();
    expect(
      screen.getByRole("button", { name: "Agregar código de proveedor" }),
    ).toBeTruthy();
  });

  it("no muestra el mensaje antiguo de usar el detalle del ítem en creación", () => {
    render(<CodesHarness />);

    expect(
      screen.queryByText("Use el detalle del ítem para revisar o mantener códigos de barras."),
    ).toBeNull();
    expect(
      screen.getByText(
        "Puede agregar el código del proveedor ahora. Después de guardar el ítem, asocie la presentación en Inventario y presentaciones.",
      ),
    ).toBeTruthy();
  });
});
