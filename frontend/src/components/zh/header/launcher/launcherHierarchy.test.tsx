// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
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

const suppliersManagementItem: NavItem = {
  id: "item-masterdata-suppliers",
  to: "/masterdata/suppliers",
  label: "Proveedores",
};
const suppliersManagementCategory: NavItem = {
  id: "category-suppliers-management",
  to: "/masterdata/suppliers/management-group",
  label: "Gestión",
  children: [suppliersManagementItem],
};
const suppliersModuleGroup: MainMenuGroup = {
  id: "suppliers",
  label: "Proveedores",
  icon: "purchases",
  isActive: false,
  items: [suppliersManagementCategory],
};
const kardexItem: NavItem = {
  id: "item-inventory-kardex",
  to: "/inventory/kardex",
  label: "Historial de Existencias",
};
const inventoryAdjustmentsItem: NavItem = {
  id: "item-inventory-adjustments",
  to: "/inventory/adjustments",
  label: "Ajustes de inventario",
};
const inventoryOperationCategory: NavItem = {
  id: "category-inventory-operation",
  to: "/inventory/operation-group",
  label: "Operación",
  children: [kardexItem, inventoryAdjustmentsItem],
};
const inventoryModuleGroup: MainMenuGroup = {
  id: "inventory",
  label: "Inventario",
  icon: "inventory",
  isActive: true,
  items: [inventoryOperationCategory],
};

function renderModule(
  overrides?: Partial<Parameters<typeof LauncherModuleGroup>[0]>,
) {
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

function renderModules({
  currentPath = "/inventory/kardex",
  expandedModuleId = "inventory",
  expandedGroupId = "inventory:category-inventory-operation",
  groups = [suppliersModuleGroup, inventoryModuleGroup],
}: {
  currentPath?: string;
  expandedModuleId?: string | null;
  expandedGroupId?: string | null;
  groups?: MainMenuGroup[];
} = {}) {
  return render(
    <div>
      {groups.map((group) => (
        <LauncherModuleGroup
          key={group.id}
          group={group}
          currentPath={currentPath}
          onNavigate={vi.fn()}
          isFavorite={() => false}
          toggleFavorite={vi.fn()}
          t={t}
          expandedModuleId={expandedModuleId}
          onToggleModule={vi.fn()}
          expandedGroupId={expandedGroupId}
          onToggleGroup={vi.fn()}
        />
      ))}
    </div>,
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

    const sibling = screen
      .getByText("Recepción electrónica (TXT)")
      .closest("a");
    expect(sibling?.getAttribute("href")).toBe("/purchases/reception");
  });

  it("marca aria-current='page' solo en la pantalla que coincide con currentPath", () => {
    renderModule();

    const activeLink = screen.getByText("Facturas de compra").closest("a");
    expect(activeLink?.getAttribute("aria-current")).toBe("page");

    const inactiveLink = screen
      .getByText("Recepción electrónica (TXT)")
      .closest("a");
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

describe("App Launcher — estados current/open/hover (ZH-MENU-ACTIVE-STATE-FIX-01)", () => {
  it("cuando la ruta actual pertenece a Inventario, Inventario es current y Proveedores no", () => {
    renderModules();

    const inventoryToggle = screen.getByRole("button", { name: "Inventario" });
    const suppliersToggle = screen.getByRole("button", { name: "Proveedores" });

    expect(inventoryToggle.className).toContain("is-current");
    expect(
      inventoryToggle
        .closest(".zh-launcher__module")
        ?.getAttribute("data-current"),
    ).toBe("true");
    expect(suppliersToggle.className).not.toContain("is-current");
    expect(
      suppliersToggle
        .closest(".zh-launcher__module")
        ?.getAttribute("data-current"),
    ).toBeNull();
  });

  it("cuando Inventario está expandido, muestra sus grupos y Proveedores no queda visualmente current", () => {
    renderModules();

    expect(screen.getByText("Operación")).toBeTruthy();
    expect(screen.getByText("Historial de Existencias")).toBeTruthy();

    const inventoryModule = screen
      .getByRole("button", { name: "Inventario" })
      .closest(".zh-launcher__module");
    const suppliersModule = screen
      .getByRole("button", { name: "Proveedores" })
      .closest(".zh-launcher__module");

    expect(inventoryModule?.className).toContain("is-open");
    expect(inventoryModule?.getAttribute("data-state")).toBe("open");
    expect(suppliersModule?.className).not.toContain("is-current");
    expect(suppliersModule?.getAttribute("data-current")).toBeNull();
  });

  it("cuando un formulario de Inventario está activo, el formulario tiene aria-current y solo su módulo padre es current", () => {
    renderModules();

    const kardexLink = screen
      .getByText("Historial de Existencias")
      .closest("a");
    const adjustmentsLink = screen
      .getByText("Ajustes de inventario")
      .closest("a");
    const currentModules = document.querySelectorAll(
      ".zh-launcher__module.is-current",
    );

    expect(kardexLink?.getAttribute("aria-current")).toBe("page");
    expect(kardexLink?.closest(".zh-launcher__item")?.className).toContain(
      "is-current",
    );
    expect(adjustmentsLink?.getAttribute("aria-current")).toBeNull();
    expect(
      screen.getByRole("button", { name: "Inventario" }).className,
    ).toContain("is-current");
    expect(
      screen.getByRole("button", { name: "Proveedores" }).className,
    ).not.toContain("is-current");
    expect(currentModules).toHaveLength(1);
  });

  it("hover/focus sobre un formulario no cambia aria-current ni el módulo current", () => {
    renderModules();

    const kardexLink = screen
      .getByText("Historial de Existencias")
      .closest("a");
    const suppliersToggle = screen.getByRole("button", { name: "Proveedores" });
    const inventoryToggle = screen.getByRole("button", { name: "Inventario" });

    expect(kardexLink).toBeTruthy();
    fireEvent.mouseOver(kardexLink!);
    fireEvent.focus(kardexLink!);

    expect(kardexLink?.getAttribute("aria-current")).toBe("page");
    expect(inventoryToggle.className).toContain("is-current");
    expect(suppliersToggle.className).not.toContain("is-current");
    expect(
      document.querySelectorAll(".zh-launcher__module.is-current"),
    ).toHaveLength(1);
  });

  it("si otro módulo está expandido pero la ruta pertenece a Proveedores, open y current se mantienen separados", () => {
    renderModules({
      currentPath: "/masterdata/suppliers",
      groups: [
        { ...suppliersModuleGroup, isActive: true },
        { ...inventoryModuleGroup, isActive: false },
      ],
      expandedModuleId: "inventory",
      expandedGroupId: "inventory:category-inventory-operation",
    });

    const suppliersModule = screen
      .getByRole("button", { name: "Proveedores" })
      .closest(".zh-launcher__module");
    const inventoryModule = screen
      .getByRole("button", { name: "Inventario" })
      .closest(".zh-launcher__module");

    expect(suppliersModule?.className).toContain("is-current");
    expect(suppliersModule?.className).not.toContain("is-open");
    expect(inventoryModule?.className).toContain("is-open");
    expect(inventoryModule?.className).not.toContain("is-current");
    expect(
      screen.getByRole("button", { name: "Inventario" }).className,
    ).not.toContain("is-current");
    expect(
      document.querySelectorAll(".zh-launcher__module.is-current"),
    ).toHaveLength(1);
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
