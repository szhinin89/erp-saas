// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  render,
  screen,
  waitFor,
  fireEvent,
  cleanup,
  act,
} from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ItemFormTabs } from "../../../modules/items/components/ItemForm/ItemFormTabs";
import { itemService } from "../../../modules/items/api/itemService";
import {
  existingItem,
  fieldByName,
  renderModal,
  waitForCatalogs,
} from "./__tests__/itemEditorModalFixture";

vi.mock("../../../modules/items/api/itemService", () => ({
  itemService: { create: vi.fn(), update: vi.fn(), getById: vi.fn(), getProfitability: vi.fn().mockResolvedValue(null) },
}));

vi.mock("../../../modules/pricing/facades/priceListLookupFacade", () => ({
  priceListLookupFacade: { list: vi.fn().mockResolvedValue([]) },
}));
vi.mock("../../../modules/configuracion/empresa/facades/companyProfileLookupFacade", () => ({
  companyProfileLookupFacade: { getProfile: vi.fn().mockResolvedValue({ currencyCode: "USD" }) },
}));

const typeCatalog = vi.hoisted(() => ({ promise: null as Promise<{ id: string; name: string }[]> | null }));
vi.mock("../../../modules/items/hooks/useItemTypeOptions", async () => {
  const { useAsync } = await import("../../../hooks/useAsync");
  return { useItemTypeOptions: () => useAsync(() => typeCatalog.promise ?? Promise.resolve([{ id: "type-1", name: "Bien" }])) };
});

vi.mock("../../../modules/lib/apiEnvelope", () => ({
  apiGet: vi.fn((url: string) => {
    if (url.includes("brands"))
      return Promise.resolve([{ id: "brand-1", name: "Marca X" }, { id: "brand-2", name: "Marca Y" }]);
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

beforeEach(() => {
  vi.clearAllMocks();
  typeCatalog.promise = null;
});

afterEach(() => {
  cleanup();
});

describe("ItemEditorModal — modo Actualizar (itemId presente)", () => {
  it("precarga sku/nombre/descripción/IVA/precio desde el Item existente", async () => {
    vi.mocked(itemService.getById).mockResolvedValue(existingItem as never);
    const { container } = renderModal({ itemId: "item-1" });

    await waitFor(() =>
      expect(itemService.getById).toHaveBeenCalledWith("item-1"),
    );
    await waitForCatalogs();

    expect(screen.getByText("Actualizar ítem existente")).toBeTruthy();
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
        screen.getByRole("button", { name: "Actualizar ítem" }),
      ).toBeTruthy(),
    );
    await waitFor(() => expect(document.querySelector<HTMLSelectElement>('[name="purchaseVatCode"]')?.value).toBe("2"));
    fireEvent.click(screen.getByRole("button", { name: "Actualizar ítem" }));

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
      expect(screen.getByText("Actualizar ítem existente")).toBeTruthy(),
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

  it('botón principal es "Actualizar ítem", nunca "Crear ítem"', async () => {
    vi.mocked(itemService.getById).mockResolvedValue(existingItem as never);
    renderModal({ itemId: "item-1" });

    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Actualizar ítem" }),
      ).toBeTruthy(),
    );
    expect(screen.queryByRole("button", { name: "Crear ítem" })).toBeNull();
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
    await waitFor(() => expect(document.querySelector<HTMLSelectElement>('[name="purchaseVatCode"]')?.value).toBe("2"));
    fireEvent.click(screen.getByRole("button", { name: "Actualizar ítem" }));

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
        screen.getByRole("button", { name: "Actualizar ítem" }),
      ).toBeTruthy(),
    );
    await waitFor(() => expect(document.querySelector<HTMLSelectElement>('[name="purchaseVatCode"]')?.value).toBe("2"));
    fireEvent.click(screen.getByRole("button", { name: "Actualizar ítem" }));

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
    await waitFor(() => expect(document.querySelector<HTMLSelectElement>('[name="purchaseVatCode"]')?.value).toBe("2"));
    fireEvent.click(screen.getByRole("button", { name: "Actualizar ítem" }));

    await waitFor(() => {
      expect(itemService.update).toHaveBeenCalledWith(
        expect.objectContaining({ baseSalePrice: 20 }),
      );
    });
  });
});


describe("ItemEditorModal — regresión catálogo de tipos asíncrono", () => {
  it.each(["Productos", "Compras"])("%s: un catálogo tardío no sobrescribe una marca ya editada", async (flow) => {
    let resolveTypes!: (types: { id: string; name: string }[]) => void;
    typeCatalog.promise = new Promise(resolve => { resolveTypes = resolve; });
    vi.mocked(itemService.getById).mockResolvedValue(existingItem);
    const view = flow === "Productos"
      ? render(<I18nProvider><ItemFormTabs itemId={existingItem.id} submitting={false} onSubmit={vi.fn()} /></I18nProvider>)
      : renderModal({ itemId: existingItem.id });
    await waitFor(() => expect(fieldByName(view.container, "shortName").value).toBe("Aceite"));
    await waitFor(() => expect(view.container.querySelector('[name="brandId"] option[value="brand-2"]')).not.toBeNull());
    expect(fieldByName(view.container, "brandId").disabled).toBe(false);
    fireEvent.change(fieldByName(view.container, "brandId"), { target: { value: "brand-2" } });
    await waitFor(() => expect(fieldByName(view.container, "brandId").value).toBe("brand-2"));
    await act(async () => resolveTypes([{ id: "type-1", name: "Bien" }]));
    await waitFor(() => expect(fieldByName(view.container, "itemTypeId").value).toBe("type-1"));
    expect(fieldByName(view.container, "brandId").value).toBe("brand-2");
  });

  it("actualiza sin exigir marca/categoría opcionales del contrato de edición", async () => {
    vi.mocked(itemService.getById).mockResolvedValue({ ...existingItem, brandId: null, categoryNodeId: null });
    vi.mocked(itemService.update).mockResolvedValue(existingItem);
    const view = renderModal({ itemId: existingItem.id });
    await waitFor(() => expect(fieldByName(view.container, "itemTypeId").value).toBe("type-1"));
    await waitFor(() => expect(document.querySelector<HTMLSelectElement>('[name="purchaseVatCode"]')?.value).toBe("2"));
    fireEvent.click(screen.getByRole("button", { name: "Actualizar ítem" }));
    await waitFor(() => expect(itemService.update).toHaveBeenCalledWith(expect.objectContaining({ brandId: null, categoryNodeId: null })));
  });
  it("Productos y Compras muestran las mismas etiquetas y valores al consultar el mismo DTO", async () => {
    vi.mocked(itemService.getById).mockResolvedValue(existingItem);
    const products = render(<I18nProvider><ItemFormTabs itemId={existingItem.id} submitting={false} disabled onSubmit={vi.fn()} /></I18nProvider>);
    await waitFor(() => expect(fieldByName(products.container, "itemTypeId").value).toBe("type-1"));
    const fields = ["sku", "shortName", "description", "itemTypeId", "nature", "defaultUomCode", "brandId", "categoryNodeId"];
    const values = fields.map(name => fieldByName(products.container, name).value);
    const labels = fields.map(name => fieldByName(products.container, name).closest("label")?.querySelector(".zh-field-label")?.textContent?.replaceAll("*", "").trim());
    for (const name of fields) expect(fieldByName(products.container, name).disabled).toBe(true);
    products.unmount();
    const purchases = renderModal({ itemId: existingItem.id });
    await waitFor(() => expect(fieldByName(purchases.container, "itemTypeId").value).toBe("type-1"));
    expect(fields.map(name => fieldByName(purchases.container, name).value)).toEqual(values);
    expect(fields.map(name => fieldByName(purchases.container, name).closest("label")?.querySelector(".zh-field-label")?.textContent?.replaceAll("*", "").trim())).toEqual(labels);
  });
  it("muestra el tipo persistido cuando el catálogo llega después del DTO y permite guardar la edición", async () => {
    let resolveTypes!: (types: { id: string; name: string }[]) => void;
    typeCatalog.promise = new Promise(resolve => { resolveTypes = resolve; });
    vi.mocked(itemService.getById).mockResolvedValue(existingItem);
    vi.mocked(itemService.update).mockResolvedValue({ ...existingItem, shortName: "Aceite editado" });
    const { container } = renderModal({ itemId: existingItem.id });
    await waitFor(() => expect(fieldByName(container, "shortName").value).toBe("Aceite"));
    expect(fieldByName(container, "itemTypeId").disabled).toBe(true);
    await act(async () => resolveTypes([{ id: "type-1", name: "Bien" }]));
    await waitFor(() => expect(fieldByName(container, "itemTypeId").value).toBe("type-1"));
    expect((fieldByName(container, "itemTypeId") as unknown as HTMLSelectElement).selectedOptions[0].text).toBe("Bien");
    expect(fieldByName(container, "nature").value).toBe("Product");
    fireEvent.change(fieldByName(container, "shortName"), { target: { value: "Aceite editado" } });
    await waitFor(() => expect(document.querySelector<HTMLSelectElement>('[name="purchaseVatCode"]')?.value).toBe("2"));
    fireEvent.click(screen.getByRole("button", { name: "Actualizar ítem" }));
    await waitFor(() => expect(itemService.update).toHaveBeenCalledWith(expect.objectContaining({
      shortName: "Aceite editado", sku: "SKU-1", defaultUomCode: "UNIT", brandId: "brand-1", categoryNodeId: "cat-1", saleVatCode: "2", purchaseVatCode: "2",
    })));
    expect(vi.mocked(itemService.update).mock.calls[0][0]).not.toHaveProperty("itemTypeId");
    expect(vi.mocked(itemService.update).mock.calls[0][0]).not.toHaveProperty("nature");
  });

  it("reabre el mismo ítem conservando tipo, naturaleza y valores actualizados", async () => {
    vi.mocked(itemService.getById).mockResolvedValue({ ...existingItem, shortName: "Aceite reabierto", nature: "Service" });
    const first = renderModal({ itemId: existingItem.id });
    await waitFor(() => expect(fieldByName(first.container, "shortName").value).toBe("Aceite reabierto"));
    first.unmount();
    let resolveTypes!: (types: { id: string; name: string }[]) => void;
    typeCatalog.promise = new Promise(resolve => { resolveTypes = resolve; });
    const second = renderModal({ itemId: existingItem.id });
    await waitFor(() => expect(fieldByName(second.container, "shortName").value).toBe("Aceite reabierto"));
    await act(async () => resolveTypes([{ id: "type-1", name: "Bien" }]));
    await waitFor(() => expect(fieldByName(second.container, "itemTypeId").value).toBe("type-1"));
    expect(fieldByName(second.container, "nature").value).toBe("Service");
    expect(fieldByName(second.container, "nature").disabled).toBe(true);
    expect(fieldByName(second.container, "saleVatCode").value).toBe("2");
    expect(fieldByName(second.container, "purchaseVatCode").value).toBe("2");
  });
});
