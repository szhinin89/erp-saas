// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { FormProvider, useForm } from "react-hook-form";
import { MemoryRouter } from "react-router-dom";
import { InventoryTab } from "./InventoryTab";
import type { CreateItemFormValues } from "../../schemas/createItemSchema";

afterEach(() => cleanup());

const t = (_key: string, fallback?: string) => fallback ?? _key;

function InventoryHarness() {
  const form = useForm<CreateItemFormValues>({
    defaultValues: {
      stockConfig: {
        tracksStock: true,
        tracksLot: false,
        tracksSeries: false,
        allowDecimalQty: false,
        allowDecimalSale: false,
        minStockQty: null,
        maxStockQty: null,
      },
    } as CreateItemFormValues,
  });

  return (
    <MemoryRouter>
      <FormProvider {...form}>
        <InventoryTab
          t={t}
          disabled={false}
          isEditMode={false}
          unitConversions={[]}
        />
      </FormProvider>
    </MemoryRouter>
  );
}

describe("InventoryTab", () => {
  it("conserva todos los switches de configuración de inventario", () => {
    render(<InventoryHarness />);

    expect(screen.getByText("Maneja stock")).toBeTruthy();
    expect(screen.getByText("Rastreo por lote")).toBeTruthy();
    expect(screen.getByText("Rastreo por serie")).toBeTruthy();
    expect(screen.getByText("Cantidades decimales")).toBeTruthy();
    expect(screen.getByText("Venta en decimales")).toBeTruthy();
    expect(screen.getAllByRole("switch")).toHaveLength(5);
  });

  it("aplica la clase responsive de opciones y no usa estilos inline", () => {
    const { container } = render(<InventoryHarness />);

    const optionGrid = screen
      .getByText("Maneja stock")
      .closest(".items-option-grid");
    expect(optionGrid).toBeTruthy();
    expect(optionGrid?.querySelectorAll(".zh-toggle")).toHaveLength(5);
    expect(container.querySelector("[style]")).toBeNull();
  });
});
