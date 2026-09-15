// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import {
  collectActiveTrailGroupKeys,
  type NavItem,
} from "../../../../nav/navConfig";
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
      expandedGroupIds={new Set(["suppliers:category-purchases"])}
      onToggleGroup={vi.fn()}
      {...overrides}
    />,
  );
}

function renderModules({
  currentPath = "/inventory/kardex",
  expandedModuleId = "inventory",
  expandedGroupIds = new Set(["inventory:category-inventory-operation"]),
  groups = [suppliersModuleGroup, inventoryModuleGroup],
}: {
  currentPath?: string;
  expandedModuleId?: string | null;
  expandedGroupIds?: ReadonlySet<string>;
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
          expandedGroupIds={expandedGroupIds}
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
      expandedGroupIds: new Set(["inventory:category-inventory-operation"]),
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

const salesCollectionItem: NavItem = {
  id: "item-sales-collection-destinations",
  to: "/accounting/configuration/sales-collection-destinations",
  label: "Cobros de ventas",
};
const destinationsCategory: NavItem = {
  id: "category-accounting-destinations",
  to: "/accounting/destinations/group",
  label: "Destinos contables",
  children: [salesCollectionItem],
};
const configurationCategory: NavItem = {
  id: "category-accounting-configuration",
  to: "/accounting/configuration/group",
  label: "Configuración",
  children: [destinationsCategory],
};
const accountingModuleGroup: MainMenuGroup = {
  id: "accounting",
  label: "Contabilidad",
  icon: "accounting",
  isActive: true,
  items: [configurationCategory],
};
const CONFIG_GROUP_KEY = "accounting:category-accounting-configuration";
const DESTINATIONS_GROUP_KEY = "accounting:category-accounting-destinations";

describe("App Launcher — N niveles sin límite (ZH-MENU-N-LEVEL-EXPAND-FIX-01)", () => {
  /**
   * Antes, `expandedGroupId` era un único string compartido por TODO el árbol: solo una
   * categoría (a cualquier profundidad) podía estar "abierta" a la vez. Abrir "Destinos
   * contables" (hija) forzaba a cerrar "Configuración" (padre) porque ambas competían por el
   * mismo valor — y al cerrarse el padre, su cuerpo deja de renderizarse, así que el hijo
   * (y "Cobros de ventas" dentro de él) desaparecía. Con el fix, `expandedGroupIds` es un Set:
   * cualquier cantidad de categorías, en cualquier profundidad, pueden estar abiertas juntas.
   */
  it("renderiza 4 niveles (Módulo > Configuración > Destinos contables > Cobros de ventas) con ambas categorías anidadas abiertas a la vez", () => {
    render(
      <LauncherModuleGroup
        group={accountingModuleGroup}
        currentPath="/accounting/configuration/sales-collection-destinations"
        onNavigate={vi.fn()}
        isFavorite={() => false}
        toggleFavorite={vi.fn()}
        t={t}
        expandedModuleId="accounting"
        onToggleModule={vi.fn()}
        expandedGroupIds={new Set([CONFIG_GROUP_KEY, DESTINATIONS_GROUP_KEY])}
        onToggleGroup={vi.fn()}
      />,
    );

    expect(screen.getByText("Contabilidad")).toBeTruthy();
    expect(screen.getByText("Configuración")).toBeTruthy();
    expect(screen.getByText("Destinos contables")).toBeTruthy();
    expect(screen.getByText("Cobros de ventas")).toBeTruthy();

    const link = screen.getByText("Cobros de ventas").closest("a");
    expect(link?.getAttribute("href")).toBe(
      "/accounting/configuration/sales-collection-destinations",
    );
    expect(link?.getAttribute("aria-current")).toBe("page");
  });

  it("el click en la pantalla hoja (nivel 4) dispara onNavigate con /accounting/configuration/sales-collection-destinations", () => {
    const onNavigate = vi.fn();
    render(
      <LauncherModuleGroup
        group={accountingModuleGroup}
        currentPath="/accounting/configuration/sales-collection-destinations"
        onNavigate={onNavigate}
        isFavorite={() => false}
        toggleFavorite={vi.fn()}
        t={t}
        expandedModuleId="accounting"
        onToggleModule={vi.fn()}
        expandedGroupIds={new Set([CONFIG_GROUP_KEY, DESTINATIONS_GROUP_KEY])}
        onToggleGroup={vi.fn()}
      />,
    );
    fireEvent.click(screen.getByText("Cobros de ventas"));
    expect(onNavigate).toHaveBeenCalledWith(
      "/accounting/configuration/sales-collection-destinations",
    );
  });

  it("si solo la categoría padre está en expandedGroupIds, la hija se ve pero no su contenido (cada nivel abre de forma independiente)", () => {
    render(
      <LauncherModuleGroup
        group={accountingModuleGroup}
        currentPath="/accounting/configuration/sales-collection-destinations"
        onNavigate={vi.fn()}
        isFavorite={() => false}
        toggleFavorite={vi.fn()}
        t={t}
        expandedModuleId="accounting"
        onToggleModule={vi.fn()}
        expandedGroupIds={new Set([CONFIG_GROUP_KEY])}
        onToggleGroup={vi.fn()}
      />,
    );

    expect(screen.getByText("Destinos contables")).toBeTruthy();
    expect(screen.queryByText("Cobros de ventas")).toBeNull();
  });

  it("collectActiveTrailGroupKeys devuelve las 2 categorías del camino activo, sin límite de profundidad", () => {
    const keys = collectActiveTrailGroupKeys(
      accountingModuleGroup.items,
      "accounting",
      "/accounting/configuration/sales-collection-destinations",
    );
    expect(keys).toEqual([CONFIG_GROUP_KEY, DESTINATIONS_GROUP_KEY]);
  });

  it("collectActiveTrailGroupKeys no incluye categorías fuera del camino activo", () => {
    const keys = collectActiveTrailGroupKeys(
      accountingModuleGroup.items,
      "accounting",
      "/accounting/journal-entries",
    );
    expect(keys).toEqual([]);
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
