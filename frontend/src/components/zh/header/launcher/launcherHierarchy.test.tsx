// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import type { NavItem } from "../../../../nav/navConfig";
import type { MainMenuGroup } from "../../../useAppLayoutNavigation";
import { LauncherModuleGroup } from "./LauncherModuleGroup";
import { LauncherFavoritesSection } from "./LauncherFavoritesSection";

/**
 * ZH-MENU-UX-DESIGN-02: este ticket es solo visual (CSS/estados) sobre el árbol ya
 * aprobado por ZH-MENU-TAXONOMY-STANDARD-01. Estas pruebas fijan el contrato que no
 * debe romperse al tocar CSS/markup: 3 niveles presentes, labels/rutas intactos,
 * ningún NavItem perdido, favorito solo en pantallas (nivel 3), y aria-current en la
 * pantalla activa.
 */

afterEach(() => cleanup());

const t = (key: string) => key;

const screenItem: NavItem = {
  id: "item-invoices",
  to: "/purchases",
  label: "Facturas de compra",
};
const screenItemSibling: NavItem = {
  id: "item-reception",
  to: "/purchases/reception",
  label: "Recepción electrónica (TXT)",
};
const categoryItem: NavItem = {
  id: "category-purchases",
  to: "/purchases/operation-group",
  label: "Compras",
  children: [screenItem, screenItemSibling],
};
const moduleGroup: MainMenuGroup = {
  id: "suppliers",
  label: "Proveedores",
  icon: "purchases",
  isActive: true,
  items: [categoryItem],
};

function renderModule(overrides?: Partial<Parameters<typeof LauncherModuleGroup>[0]>) {
  return render(
    <LauncherModuleGroup
      group={moduleGroup}
      currentPath="/purchases"
      onNavigate={vi.fn()}
      isFavorite={() => false}
      toggleFavorite={vi.fn()}
      t={t}
      expandedModuleId="suppliers"
      onToggleModule={vi.fn()}
      expandedGroupId="suppliers:category-purchases"
      onToggleGroup={vi.fn()}
      {...overrides}
    />,
  );
}

describe("App Launcher — jerarquía de 3 niveles (ZH-MENU-UX-DESIGN-02)", () => {
  it("renderiza el módulo (nivel 1), la categoría (nivel 2) y la pantalla (nivel 3) sin perder ningún NavItem", () => {
    renderModule();

    expect(screen.getByText("Proveedores")).toBeTruthy();
    expect(screen.getByText("Compras")).toBeTruthy();
    expect(screen.getByText("Facturas de compra")).toBeTruthy();
    expect(screen.getByText("Recepción electrónica (TXT)")).toBeTruthy();
  });

  it("no cambia las rutas: cada link apunta exactamente a item.to", () => {
    renderModule();

    const link = screen.getByText("Facturas de compra").closest("a");
    expect(link?.getAttribute("href")).toBe("/purchases");

    const sibling = screen.getByText("Recepción electrónica (TXT)").closest("a");
    expect(sibling?.getAttribute("href")).toBe("/purchases/reception");
  });

  it("marca aria-current='page' solo en la pantalla que coincide con currentPath", () => {
    renderModule();

    const activeLink = screen.getByText("Facturas de compra").closest("a");
    expect(activeLink?.getAttribute("aria-current")).toBe("page");

    const inactiveLink = screen.getByText("Recepción electrónica (TXT)").closest("a");
    expect(inactiveLink?.getAttribute("aria-current")).toBeNull();
  });

  it("solo la pantalla (nivel 3) muestra el botón de favorito — módulo y categoría no", () => {
    renderModule();

    // Dos pantallas hoja → dos botones de favorito (aria-pressed).
    const favButtons = screen.getAllByRole("button", { name: /favorit/i });
    expect(favButtons).toHaveLength(2);

    // El toggle del módulo y el de la categoría no exponen aria-pressed (no son favoritos).
    const moduleToggle = screen.getByRole("button", { name: "Proveedores" });
    expect(moduleToggle.getAttribute("aria-pressed")).toBeNull();

    const categoryToggle = screen.getByRole("button", { name: "Compras" });
    expect(categoryToggle.getAttribute("aria-pressed")).toBeNull();
  });

  it("favorito activo se refleja de forma persistente vía aria-pressed/is-on", () => {
    renderModule({ isFavorite: (id) => id === "item-invoices" });

    const favOn = screen.getByRole("button", {
      name: /Facturas de compra/i,
    });
    expect(favOn.getAttribute("aria-pressed")).toBe("true");
    expect(favOn.className).toContain("is-on");

    const favOff = screen.getByRole("button", {
      name: /Recepción electrónica/i,
    });
    expect(favOff.getAttribute("aria-pressed")).toBe("false");
    expect(favOff.className).not.toContain("is-on");
  });
});

describe("App Launcher — Mis accesos rápidos (ZH-MENU-UX-DESIGN-02)", () => {
  it("sigue renderizando los favoritos con su contador, sin cambiar rutas", () => {
    render(
      <LauncherFavoritesSection
        favorites={[screenItem, screenItemSibling]}
        currentPath="/purchases"
        onNavigate={vi.fn()}
        isFavorite={() => true}
        toggleFavorite={vi.fn()}
        t={t}
      />,
    );

    expect(screen.getByText("2")).toBeTruthy();
    const link = screen.getByText("Facturas de compra").closest("a");
    expect(link?.getAttribute("href")).toBe("/purchases");
  });
});
