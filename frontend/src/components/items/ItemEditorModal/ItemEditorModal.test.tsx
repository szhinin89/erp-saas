// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  render,
  screen,
  waitFor,
  fireEvent,
  cleanup,
} from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ItemEditorModal } from "./ItemEditorModal";
import { itemService } from "../../../modules/items/api/itemService";
import { apiGet } from "../../../modules/lib/apiEnvelope";
import type { CreateItemInitialData, ItemCreatedResult } from "./types";
import type { ItemDetailDto } from "../../../types/items";

vi.mock("../../../modules/items/api/itemService", () => ({
  itemService: { create: vi.fn(), update: vi.fn(), getById: vi.fn() },
}));

vi.mock("../../../modules/items/hooks/useItemTypeOptions", () => ({
  useItemTypeOptions: () => ({ data: [{ id: "type-1", name: "Bien" }] }),
}));

vi.mock("../../../modules/lib/apiEnvelope", () => ({
  apiGet: vi.fn((url: string) => {
    if (url.includes("brands"))
      return Promise.resolve([{ id: "brand-1", name: "Marca X" }]);
    if (url.includes("category-nodes")) {
      return Promise.resolve({
        nodes: [
          {
            id: "cat-1",
            name: "Categoría 1",
            path: "",
            parentId: null,
            isActive: true,
          },
        ],
      });
    }
    if (url.includes("sri-uom"))
      return Promise.resolve([{ code: "UNIT", name: "Unidad", abbrev: "U" }]);
    if (url.includes("barcode-types"))
      return Promise.resolve([{ code: "EAN13", name: "EAN-13" }]);
    if (url.includes("sri-vat-rates"))
      return Promise.resolve([{ code: "2", name: "IVA 15%", percentage: 15 }]);
    return Promise.resolve([]);
  }),
}));

const existingItem: ItemDetailDto = {
  id: "item-1",
  sku: "SKU-1",
  shortName: "Aceite",
  description: "Aceite girasol",
  itemTypeId: "type-1",
  itemTypeName: "Bien",
  categoryNodeId: "cat-1",
  brandId: "brand-1",
  defaultUomCode: "UNIT",
  defaultUomAbbrev: "U",
  isForSale: true,
  isFavorite: false,
  isEcommerceActive: false,
  tracksStock: true,
  tracksLot: false,
  tracksSeries: false,
  baseSalePrice: 10,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
  defaultUomName: "Unidad",
  observations: null,
  taxConfig: {
    saleVatCode: "2",
    saleVatName: "IVA 15%",
    purchaseVatCode: "2",
    purchaseVatName: "IVA 15%",
    exciseTaxCode: null,
    exciseTaxName: null,
  },
  saleConfig: {
    isForSale: true,
    maxDiscountPercent: null,
    isAvailableOnWeb: false,
    isAvailableOnPOS: false,
    isAvailableOnMobile: false,
    isEcommerceActive: false,
    isFavorite: false,
  },
  stockConfig: {
    tracksStock: true,
    tracksLot: false,
    tracksSeries: false,
    allowDecimalQty: false,
    allowDecimalSale: false,
    minStockQty: null,
    maxStockQty: null,
  },
  variants: [],
  images: [],
  unitConversions: [],
  substitutes: [],
  packagingLevels: [],
  supplierCodes: [],
};

function renderModal(
  props: {
    open?: boolean;
    itemId?: string;
    initialData?: CreateItemInitialData;
    onClose?: () => void;
    onSaved?: (item: ItemCreatedResult) => void;
  } = {},
) {
  const onClose = props.onClose ?? vi.fn();
  const onSaved = props.onSaved ?? vi.fn();
  const utils = render(
    <I18nProvider>
      <ItemEditorModal
        open={props.open ?? true}
        itemId={props.itemId}
        initialData={props.initialData}
        onClose={onClose}
        onSaved={onSaved}
      />
    </I18nProvider>,
  );
  return { ...utils, onClose, onSaved };
}

function fieldByName(container: HTMLElement, name: string): HTMLInputElement {
  const el = container.querySelector(`[name="${name}"]`);
  if (!el) throw new Error(`[name="${name}"] no encontrado`);
  return el as HTMLInputElement;
}

async function waitForCatalogs() {
  await waitFor(() => expect(vi.mocked(apiGet)).toHaveBeenCalled());
}

async function fillRequiredFields(
  container: HTMLElement,
  overrides: Partial<
    Record<"sku" | "barcode" | "saleVatCode" | "purchaseVatCode", string>
  > & { salePrice?: string } = {},
) {
  fireEvent.change(fieldByName(container, "sku"), {
    target: { value: overrides.sku ?? "SKU-1" },
  });
  fireEvent.change(fieldByName(container, "itemTypeId"), {
    target: { value: "type-1" },
  });
  fireEvent.change(fieldByName(container, "categoryNodeId"), {
    target: { value: "cat-1" },
  });
  fireEvent.change(fieldByName(container, "brandId"), {
    target: { value: "brand-1" },
  });
  fireEvent.change(fieldByName(container, "defaultUomCode"), {
    target: { value: "UNIT" },
  });
  if (overrides.barcode !== undefined) {
    fireEvent.change(fieldByName(container, "barcode"), {
      target: { value: overrides.barcode },
    });
  }
  fireEvent.change(fieldByName(container, "barcodeType"), {
    target: { value: "EAN13" },
  });
  // Obligatorios en modo Crear (fix "listo para comprar y vender sin reproceso manual"):
  // IVA de venta, IVA de compra y precio de venta.
  fireEvent.change(fieldByName(container, "saleVatCode"), {
    target: { value: overrides.saleVatCode ?? "2" },
  });
  fireEvent.change(fieldByName(container, "purchaseVatCode"), {
    target: { value: overrides.purchaseVatCode ?? "2" },
  });
  fireEvent.change(fieldByName(container, "salePrice"), {
    target: { value: overrides.salePrice ?? "10" },
  });
}

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
});

describe("ItemEditorModal — modo Crear — render", () => {
  it("no renderiza el formulario cuando open=false", () => {
    renderModal({ open: false });

    expect(screen.queryByText("Crear nuevo Item")).toBeNull();
  });

  it("prellena los campos desde initialData", async () => {
    const { container } = renderModal({
      initialData: {
        name: "ACEITE GIRASOL 1L",
        barcode: "00125",
        supplierCode: "00125",
        supplierName: "Distribuidora ABC",
      },
    });
    await waitForCatalogs();

    expect(fieldByName(container, "shortName").value).toBe("ACEITE GIRASOL 1L");
    expect(fieldByName(container, "description").value).toBe(
      "ACEITE GIRASOL 1L",
    );
    expect(fieldByName(container, "barcode").value).toBe("00125");
    expect(screen.getByDisplayValue("Distribuidora ABC")).toBeTruthy();
  });
});

describe("ItemEditorModal — modo Crear — envío", () => {
  it("éxito: llama itemService.create con el payload esperado (incluye supplierCodes) y dispara onSaved", async () => {
    vi.mocked(itemService.create).mockResolvedValue({
      id: "item-1",
      sku: "SKU-1",
      shortName: "Aceite",
    } as never);
    const { container, onSaved } = renderModal({
      initialData: {
        name: "Aceite",
        barcode: "00125",
        supplierCode: "00125",
        supplierId: "supplier-1",
      },
    });
    await waitForCatalogs();

    await fillRequiredFields(container);
    fireEvent.click(screen.getByRole("button", { name: "Crear Item" }));

    await waitFor(() => {
      expect(itemService.create).toHaveBeenCalledWith(
        expect.objectContaining({
          sku: "SKU-1",
          itemTypeId: "type-1",
          categoryNodeId: "cat-1",
          brandId: "brand-1",
          defaultUomCode: "UNIT",
          barcodes: [{ code: "00125", barcodeType: "EAN13", isPrimary: true }],
          supplierCodes: [
            { supplierId: "supplier-1", code: "00125", isPrimary: true },
          ],
        }),
      );
      expect(onSaved).toHaveBeenCalledWith({
        id: "item-1",
        sku: "SKU-1",
        shortName: "Aceite",
        baseSalePrice: undefined,
        purchaseVatCode: "2",
      });
    });
  });

  it("error del backend se muestra en el campo correspondiente del formulario, sin cerrar el modal", async () => {
    const validationError = {
      isAxiosError: true,
      response: {
        status: 422,
        data: {
          data: { errors: { sku: ["Ya existe un ítem con SKU 'SKU-1'."] } },
        },
      },
    };
    vi.mocked(itemService.create).mockRejectedValue(validationError);
    const { container, onClose, onSaved } = renderModal({
      initialData: { name: "Aceite" },
    });
    await waitForCatalogs();

    await fillRequiredFields(container, { barcode: "00125" });
    fireEvent.click(screen.getByRole("button", { name: "Crear Item" }));

    await waitFor(() =>
      expect(
        screen.getByText("Ya existe un ítem con SKU 'SKU-1'."),
      ).toBeTruthy(),
    );
    expect(onClose).not.toHaveBeenCalled();
    expect(onSaved).not.toHaveBeenCalled();
  });

  // Reproduce el envelope real de un Conflict con código no registrado en MessageCatalog
  // (SKU_DUPLICATE/BARCODE_DUPLICATE): status 400, sin mapa de campos, mensaje específico
  // solo en `data.errors` (array plano) y `message.user` genérico ("Ocurrió un error inesperado.").
  it("SKU_DUPLICATE (data.errors plano, no 422): muestra el mensaje específico del backend, no el genérico", async () => {
    const skuDuplicateError = {
      isAxiosError: true,
      response: {
        status: 400,
        data: {
          code: "SKU_DUPLICATE",
          message: {
            user: "Ocurrió un error inesperado.",
            dev: "Unmapped response code.",
          },
          data: { errors: ["Ya existe un ítem con SKU '10001204'."] },
        },
      },
    };
    vi.mocked(itemService.create).mockRejectedValue(skuDuplicateError);
    const { container, onClose, onSaved } = renderModal({
      initialData: { name: "Aceite" },
    });
    await waitForCatalogs();

    await fillRequiredFields(container, { sku: "10001204", barcode: "00125" });
    fireEvent.click(screen.getByRole("button", { name: "Crear Item" }));

    await waitFor(() =>
      expect(
        screen.getByText("Ya existe un ítem con SKU '10001204'."),
      ).toBeTruthy(),
    );
    expect(screen.queryByText("Ocurrió un error inesperado.")).toBeNull();
    expect(onClose).not.toHaveBeenCalled();
    expect(onSaved).not.toHaveBeenCalled();
  });

  it("BARCODE_DUPLICATE (data.errors plano, no 422): muestra el mensaje específico del backend", async () => {
    const barcodeDuplicateError = {
      isAxiosError: true,
      response: {
        status: 400,
        data: {
          code: "BARCODE_DUPLICATE",
          message: {
            user: "Ocurrió un error inesperado.",
            dev: "Unmapped response code.",
          },
          data: {
            errors: [
              "El código de barras '00125' ya está asignado a otro ítem.",
            ],
          },
        },
      },
    };
    vi.mocked(itemService.create).mockRejectedValue(barcodeDuplicateError);
    const { container } = renderModal({ initialData: { name: "Aceite" } });
    await waitForCatalogs();

    await fillRequiredFields(container, { barcode: "00125" });
    fireEvent.click(screen.getByRole("button", { name: "Crear Item" }));

    await waitFor(() =>
      expect(
        screen.getByText(
          "El código de barras '00125' ya está asignado a otro ítem.",
        ),
      ).toBeTruthy(),
    );
  });

  it("error inesperado sin mensaje funcional: recién ahí muestra el texto genérico", async () => {
    const unexpectedError = {
      isAxiosError: true,
      response: { status: 500, data: {} },
    };
    vi.mocked(itemService.create).mockRejectedValue(unexpectedError);
    const { container } = renderModal({ initialData: { name: "Aceite" } });
    await waitForCatalogs();

    await fillRequiredFields(container, { barcode: "00125" });
    fireEvent.click(screen.getByRole("button", { name: "Crear Item" }));

    await waitFor(() =>
      expect(screen.getByText("No se pudo crear el ítem.")).toBeTruthy(),
    );
  });
});

describe("ItemEditorModal — simulación de precio de compra (purchaseContext)", () => {
  it("sin purchaseContext: no renderiza la sección de compra (compatibilidad total)", async () => {
    renderModal({ initialData: { name: "Aceite" } });
    await waitForCatalogs();

    expect(screen.queryByText("Información de Compra")).toBeNull();
  });

  it("con purchaseContext: muestra costo, cantidad, descuento y costo final ya calculado", async () => {
    renderModal({
      initialData: {
        name: "Aceite",
        purchaseContext: { unitCost: 8.5, quantity: 10, discountPct: 15 },
      },
    });
    await waitForCatalogs();

    expect(screen.getByText("Información de Compra")).toBeTruthy();
    // Costo final = 8.50 * (1 - 15/100) = 7.225.
    expect(screen.getByDisplayValue("$8.5000")).toBeTruthy();
    expect(screen.getByDisplayValue("15.00%")).toBeTruthy();
    expect(screen.getByDisplayValue("$7.2250")).toBeTruthy();
  });

  it('descuento desconocido (undefined): muestra "—", nunca "0%"', async () => {
    renderModal({
      initialData: {
        name: "Aceite",
        purchaseContext: { unitCost: 8.5, quantity: 10 },
      },
    });
    await waitForCatalogs();

    expect(screen.getAllByDisplayValue("—").length).toBeGreaterThan(0);
  });

  it("la simulación de margen se actualiza en vivo al escribir el precio, sin backend", async () => {
    const { container } = renderModal({
      initialData: {
        name: "Aceite",
        purchaseContext: { unitCost: 8.5, quantity: 10 },
      },
    });
    await waitForCatalogs();

    fireEvent.change(fieldByName(container, "salePrice"), {
      target: { value: "12" },
    });

    // Margen = (12 - 8.5) / 12 * 100 = 29.166...% → mismo criterio que GetPurchaseItemContextQueryHandler.
    await waitFor(() => expect(screen.getByText("29.17%")).toBeTruthy());
    expect(itemService.create).not.toHaveBeenCalled();
  });

  it('IVA XML resuelve desde purchaseContext.vatCode contra el mismo catálogo del selector "IVA del Item"', async () => {
    renderModal({
      initialData: {
        name: "Aceite",
        purchaseContext: { unitCost: 8.5, quantity: 10, vatCode: "2" },
      },
    });
    await waitForCatalogs();

    await waitFor(() =>
      expect(screen.getByDisplayValue("15.00%")).toBeTruthy(),
    );
  });

  it('no muestra el checkbox "Actualizar precio" en modo Crear — el precio es obligatorio, no opcional', async () => {
    renderModal({
      initialData: {
        name: "Aceite",
        purchaseContext: { unitCost: 8.5, quantity: 10 },
      },
    });
    await waitForCatalogs();

    expect(screen.queryByText(/Actualizar precio del Item/)).toBeNull();
  });

  it("crear con precio, IVA de venta e IVA de compra: guarda baseSalePrice con el precio ingresado (obligatorio en este flujo)", async () => {
    vi.mocked(itemService.create).mockResolvedValue({
      id: "item-1",
      sku: "SKU-1",
      shortName: "Aceite",
      baseSalePrice: 12,
    } as never);
    const { container, onSaved } = renderModal({
      initialData: {
        name: "Aceite",
        purchaseContext: { unitCost: 8.5, quantity: 10 },
      },
    });
    await waitForCatalogs();

    await fillRequiredFields(container, {
      barcode: "00125",
      salePrice: "12",
    });
    fireEvent.click(screen.getByRole("button", { name: "Crear Item" }));

    await waitFor(() => {
      expect(itemService.create).toHaveBeenCalledWith(
        expect.objectContaining({
          baseSalePrice: 12,
          saleVatCode: "2",
          purchaseVatCode: "2",
        }),
      );
      expect(onSaved).toHaveBeenCalledWith(
        expect.objectContaining({ baseSalePrice: 12, purchaseVatCode: "2" }),
      );
    });
  });

  it("sin precio de venta: bloquea el envío con un error de validación", async () => {
    const { container } = renderModal({
      initialData: {
        name: "Aceite",
        purchaseContext: { unitCost: 8.5, quantity: 10 },
      },
    });
    await waitForCatalogs();

    fireEvent.change(fieldByName(container, "sku"), {
      target: { value: "SKU-1" },
    });
    fireEvent.change(fieldByName(container, "itemTypeId"), {
      target: { value: "type-1" },
    });
    fireEvent.change(fieldByName(container, "categoryNodeId"), {
      target: { value: "cat-1" },
    });
    fireEvent.change(fieldByName(container, "brandId"), {
      target: { value: "brand-1" },
    });
    fireEvent.change(fieldByName(container, "defaultUomCode"), {
      target: { value: "UNIT" },
    });
    fireEvent.change(fieldByName(container, "barcode"), {
      target: { value: "00125" },
    });
    fireEvent.change(fieldByName(container, "barcodeType"), {
      target: { value: "EAN13" },
    });
    fireEvent.change(fieldByName(container, "saleVatCode"), {
      target: { value: "2" },
    });
    fireEvent.change(fieldByName(container, "purchaseVatCode"), {
      target: { value: "2" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Crear Item" }));

    await waitFor(() =>
      expect(
        screen.getByText("Ingrese un precio de venta válido para crear el Item."),
      ).toBeTruthy(),
    );
    expect(itemService.create).not.toHaveBeenCalled();
  });

  it("sin IVA de compra: bloquea el envío con un error de validación", async () => {
    const { container } = renderModal({
      initialData: { name: "Aceite" },
    });
    await waitForCatalogs();

    await fillRequiredFields(container, {
      barcode: "00125",
      purchaseVatCode: "",
    });
    fireEvent.click(screen.getByRole("button", { name: "Crear Item" }));

    await waitFor(() =>
      expect(
        screen.getByText("Debe seleccionar el IVA de compra del Item."),
      ).toBeTruthy(),
    );
    expect(itemService.create).not.toHaveBeenCalled();
  });

  it("con purchaseContext.vatCode: precarga IVA de venta e IVA de compra sugeridos", async () => {
    const { container } = renderModal({
      initialData: {
        name: "Aceite",
        purchaseContext: { unitCost: 8.5, quantity: 10, vatCode: "2" },
      },
    });
    await waitForCatalogs();

    await waitFor(() => {
      expect(fieldByName(container, "saleVatCode").value).toBe("2");
      expect(fieldByName(container, "purchaseVatCode").value).toBe("2");
    });
  });
});

describe("ItemEditorModal — modo Actualizar (itemId presente)", () => {
  it("precarga sku/nombre/descripción/IVA/precio desde el Item existente", async () => {
    vi.mocked(itemService.getById).mockResolvedValue(existingItem as never);
    const { container } = renderModal({ itemId: "item-1" });

    await waitFor(() =>
      expect(itemService.getById).toHaveBeenCalledWith("item-1"),
    );
    await waitForCatalogs();

    expect(screen.getByText("Actualizar Item existente")).toBeTruthy();
    await waitFor(() =>
      expect(fieldByName(container, "shortName").value).toBe("Aceite"),
    );
    expect(fieldByName(container, "sku").value).toBe("SKU-1");
    expect(fieldByName(container, "description").value).toBe("Aceite girasol");
  });

  it("prellena categoría, marca, UOM, IVA de venta e IVA de compra desde el Item existente — no se pierden aunque los catálogos (fetch async) resuelvan después del reset inicial", async () => {
    vi.mocked(itemService.getById).mockResolvedValue(existingItem as never);
    const { container } = renderModal({ itemId: "item-1" });

    await waitFor(() =>
      expect(itemService.getById).toHaveBeenCalledWith("item-1"),
    );
    await waitForCatalogs();

    await waitFor(() => {
      expect(fieldByName(container, "categoryNodeId").value).toBe("cat-1");
      expect(fieldByName(container, "brandId").value).toBe("brand-1");
      expect(fieldByName(container, "defaultUomCode").value).toBe("UNIT");
      expect(fieldByName(container, "saleVatCode").value).toBe("2");
      expect(fieldByName(container, "purchaseVatCode").value).toBe("2");
    });
  });

  it("al guardar sin tocar nada, no borra categoría/marca (regresión: uncontrolled select vacío por catálogo aún no cargado)", async () => {
    vi.mocked(itemService.getById).mockResolvedValue(existingItem as never);
    vi.mocked(itemService.update).mockResolvedValue({
      id: "item-1",
      sku: "SKU-1",
      shortName: "Aceite",
      baseSalePrice: 10,
    } as never);
    renderModal({ itemId: "item-1" });

    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Actualizar Item" }),
      ).toBeTruthy(),
    );
    fireEvent.click(screen.getByRole("button", { name: "Actualizar Item" }));

    await waitFor(() => {
      expect(itemService.update).toHaveBeenCalledWith(
        expect.objectContaining({
          categoryNodeId: "cat-1",
          brandId: "brand-1",
          saleVatCode: "2",
          purchaseVatCode: "2",
        }),
      );
    });
  });

  it("no muestra los campos de código de barras (Update no gestiona códigos de barras)", async () => {
    vi.mocked(itemService.getById).mockResolvedValue(existingItem as never);
    renderModal({ itemId: "item-1" });

    await waitFor(() =>
      expect(screen.getByText("Actualizar Item existente")).toBeTruthy(),
    );
    expect(screen.queryByText("Código de barras")).toBeNull();
  });

  it("el tipo de ítem queda deshabilitado (inmutable post-creación)", async () => {
    vi.mocked(itemService.getById).mockResolvedValue(existingItem as never);
    const { container } = renderModal({ itemId: "item-1" });

    await waitFor(() =>
      expect(fieldByName(container, "itemTypeId").disabled).toBe(true),
    );
  });

  it('botón principal es "Actualizar Item", nunca "Crear Item"', async () => {
    vi.mocked(itemService.getById).mockResolvedValue(existingItem as never);
    renderModal({ itemId: "item-1" });

    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Actualizar Item" }),
      ).toBeTruthy(),
    );
    expect(screen.queryByRole("button", { name: "Crear Item" })).toBeNull();
  });

  it("éxito: llama itemService.update (nunca create) y dispara onSaved", async () => {
    vi.mocked(itemService.getById).mockResolvedValue(existingItem as never);
    vi.mocked(itemService.update).mockResolvedValue({
      id: "item-1",
      sku: "SKU-1",
      shortName: "Aceite Editado",
      baseSalePrice: 10,
    } as never);
    const { container, onSaved } = renderModal({ itemId: "item-1" });

    await waitFor(() =>
      expect(fieldByName(container, "shortName").value).toBe("Aceite"),
    );
    fireEvent.change(fieldByName(container, "shortName"), {
      target: { value: "Aceite Editado" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Actualizar Item" }));

    await waitFor(() => {
      expect(itemService.update).toHaveBeenCalledWith(
        expect.objectContaining({ id: "item-1", shortName: "Aceite Editado" }),
      );
      expect(itemService.create).not.toHaveBeenCalled();
      expect(onSaved).toHaveBeenCalledWith(
        expect.objectContaining({ id: "item-1", shortName: "Aceite Editado" }),
      );
    });
  });

  it("updatePrice desmarcado: reenvía el precio actual del Item (nunca null) — BaseSalePrice es obligatorio en Update", async () => {
    vi.mocked(itemService.getById).mockResolvedValue(existingItem as never);
    vi.mocked(itemService.update).mockResolvedValue({
      id: "item-1",
      sku: "SKU-1",
      shortName: "Aceite",
      baseSalePrice: 10,
    } as never);
    renderModal({ itemId: "item-1" });

    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Actualizar Item" }),
      ).toBeTruthy(),
    );
    fireEvent.click(screen.getByRole("button", { name: "Actualizar Item" }));

    await waitFor(() => {
      expect(itemService.update).toHaveBeenCalledWith(
        expect.objectContaining({ baseSalePrice: 10 }),
      );
    });
  });

  it("updatePrice marcado: envía el nuevo precio ingresado", async () => {
    vi.mocked(itemService.getById).mockResolvedValue(existingItem as never);
    vi.mocked(itemService.update).mockResolvedValue({
      id: "item-1",
      sku: "SKU-1",
      shortName: "Aceite",
      baseSalePrice: 20,
    } as never);
    const { container } = renderModal({ itemId: "item-1" });

    await waitFor(() =>
      expect(fieldByName(container, "shortName").value).toBe("Aceite"),
    );
    fireEvent.change(fieldByName(container, "salePrice"), {
      target: { value: "20" },
    });
    fireEvent.click(fieldByName(container, "updatePrice"));
    fireEvent.click(screen.getByRole("button", { name: "Actualizar Item" }));

    await waitFor(() => {
      expect(itemService.update).toHaveBeenCalledWith(
        expect.objectContaining({ baseSalePrice: 20 }),
      );
    });
  });
});
