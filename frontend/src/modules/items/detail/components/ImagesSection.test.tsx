// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ImagesSection } from "./ImagesSection";
import { useItemDetailPage } from "../hooks/useItemDetail";
import { itemService } from "../../api/itemService";
import { message } from "../../../../lib/messages";
import { I18nProvider } from "../../../../i18n/i18n";
import es from "../../../../i18n/locales/es.json";
import en from "../../../../i18n/locales/en.json";
import type { ItemDetailDto, ItemImageDto } from "../../../../types/items";

const access = vi.hoisted(() => ({ edit: true }));
vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: (permission: string) => permission === "items.edit" && access.edit }),
}));
vi.mock("../../api/itemService", () => ({ itemService: {
  getById: vi.fn(), enableImage: vi.fn(), disableImage: vi.fn(),
} }));
vi.mock("../../../../lib/messages", () => ({ message: { confirm: vi.fn() } }));

const inactive: ItemImageDto = { id: "img-b", storageObjectId: "fake-storage", variantId: null,
  altText: "Old image", isActive: false, isMain: true, isEcommerce: true, sortOrder: 2 };
const main = { ...inactive, id: "img-a", altText: "Current main", isActive: true, sortOrder: 1 };
const secondary = { ...inactive, id: "img-c", altText: "Secondary", isMain: false, isActive: true };
const item = { id: "item-1", images: [secondary, inactive, main] } as ItemDetailDto;
const restored = { ...inactive, isActive: true, isMain: false };
const refreshed = { ...item, images: [secondary, restored, main] };
const t = (key: string, fallback?: string) => (es as Record<string, string>)[key] ?? fallback ?? key;

function Harness({ disabled = false }: { disabled?: boolean }) {
  const detail = useItemDetailPage("item-1");
  return <ImagesSection t={t} images={detail.item?.images ?? []} disabled={disabled}
    pending={detail.statusChangePending} error={detail.imageActivationError}
    onDisable={detail.disableImage} onEnable={detail.enableImage} />;
}
async function ready() {
  render(<I18nProvider><Harness /></I18nProvider>);
  await screen.findByText("Current main");
}
function filter(value: string) {
  fireEvent.change(screen.getByRole("combobox", { name: "Estado de imágenes" }), { target: { value } });
}
const service = vi.mocked(itemService);
beforeEach(() => {
  vi.resetAllMocks(); access.edit = true;
  service.getById.mockResolvedValue(item);
  service.enableImage.mockResolvedValue(restored);
});
afterEach(cleanup);

describe("ImagesSection — state, permissions and order", () => {
  it("defaults to active and exposes inactive/all with explicit status and stable order", () => {
    render(<ImagesSection t={t} images={item.images} onDisable={vi.fn()} onEnable={vi.fn()} />);
    expect(screen.getByRole("combobox")).toHaveProperty("value", "active");
    expect(screen.queryByText("Old image")).toBeNull();
    filter("inactive");
    expect(screen.getByText("Old image")).toBeTruthy();
    expect(screen.getByText("Inactiva")).toBeTruthy();
    expect(screen.queryByText("Current main")).toBeNull();
    filter("all");
    const rows = screen.getAllByRole("row").slice(1);
    expect(rows.map((row) => within(row).getAllByRole("cell")[1].textContent))
      .toEqual(["Current main", "Old image", "Secondary"]);
    expect(item.images.map((image) => image.id)).toEqual(["img-c", "img-b", "img-a"]);
    expect(screen.getAllByText("Activa")).toHaveLength(2);
  });

  it("keeps the filter available when no active images exist", () => {
    render(<ImagesSection t={t} images={[inactive]} onDisable={vi.fn()} onEnable={vi.fn()} />);
    expect(screen.getByText("No hay imágenes.")).toBeTruthy();
    filter("inactive");
    expect(screen.getByText("Old image")).toBeTruthy();
  });

  it.each([[false, false], [true, true], [false, true]])(
    "hides activation without edit permission or in read-only mode (edit=%s, readOnly=%s)", (edit, disabled) => {
      access.edit = edit;
      const onEnable = vi.fn();
      render(<ImagesSection t={t} images={item.images} disabled={disabled} onDisable={vi.fn()} onEnable={onEnable} />);
      filter("inactive");
      expect(screen.queryByRole("button", { name: "Activar" })).toBeNull();
      expect(onEnable).not.toHaveBeenCalled();
    });

  it.each([es, en])("renders translated controls and hint", (dictionary) => {
    const translate = (key: string) => (dictionary as Record<string, string>)[key] ?? key;
    render(<ImagesSection t={translate} images={[inactive]} onDisable={vi.fn()} onEnable={vi.fn()} />);
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "inactive" } });
    expect(screen.getByRole("button", { name: dictionary["items.images.activate"] })).toBeTruthy();
    expect(screen.getByText(dictionary["items.images.activate.hint"])).toBeTruthy();
  });
});

describe("Image activation — real component and hook", () => {
  it("sends one request without confirmation, retains row during mutation/refresh, preserves main", async () => {
    let finishMutation!: (image: ItemImageDto) => void;
    let finishRefresh!: (detail: ItemDetailDto) => void;
    service.enableImage.mockReturnValue(new Promise((resolve) => { finishMutation = resolve; }));
    service.getById.mockResolvedValueOnce(item).mockReturnValueOnce(new Promise((resolve) => { finishRefresh = resolve; }));
    await ready(); filter("all");
    const button = screen.getByRole("button", { name: "Activar" });
    fireEvent.click(button); fireEvent.click(button);
    expect(service.enableImage).toHaveBeenCalledTimes(1);
    expect(service.enableImage).toHaveBeenCalledWith("item-1", "img-b");
    expect(message.confirm).not.toHaveBeenCalled();
    expect(screen.getByText("Old image")).toBeTruthy();
    expect(button).toHaveProperty("disabled", true);
    await act(async () => { finishMutation(restored); });
    expect(screen.getByText("Old image")).toBeTruthy();
    expect(button).toHaveProperty("disabled", true);
    await act(async () => { finishRefresh(refreshed); });
    await waitFor(() => expect(screen.queryByRole("button", { name: "Activar" })).toBeNull());
    const row = screen.getByText("Current main").closest("tr")!;
    expect(within(row).getAllByRole("cell")[3].textContent).toBe("Sí");
    expect(within(screen.getByText("Old image").closest("tr")!).getAllByRole("cell")[3].textContent).toBe("No");
    expect(service.getById).toHaveBeenCalledTimes(2);
  });

  it("shows the official backend error, retains row and allows retry", async () => {
    service.enableImage.mockRejectedValueOnce({ isAxiosError: true,
      response: { status: 404, data: { code: "NOT_FOUND", message: { user: "Imagen no encontrada." } } } });
    await ready(); filter("inactive");
    fireEvent.click(screen.getByRole("button", { name: "Activar" }));
    expect(await screen.findByRole("alert")).toHaveProperty("textContent", expect.stringContaining("Imagen no encontrada."));
    expect(screen.getByText("Old image")).toBeTruthy();
    expect(service.getById).toHaveBeenCalledTimes(1);
    service.getById.mockResolvedValue(refreshed);
    fireEvent.click(screen.getByRole("button", { name: "Activar" }));
    await waitFor(() => expect(screen.queryByText("Old image")).toBeNull());
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("reports a saved activation with failed refresh and keeps the last server snapshot", async () => {
    service.getById.mockResolvedValueOnce(item).mockRejectedValueOnce({ isAxiosError: true,
      response: { status: 503, data: { message: { user: "Servicio no disponible." } } } });
    await ready(); filter("inactive");
    fireEvent.click(screen.getByRole("button", { name: "Activar" }));
    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("La activación se guardó");
    expect(alert.textContent).toContain("Servicio no disponible.");
    expect(screen.getByText("Old image")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Activar" })).toHaveProperty("disabled", false);
    expect(message.confirm).not.toHaveBeenCalled();
  });
});
