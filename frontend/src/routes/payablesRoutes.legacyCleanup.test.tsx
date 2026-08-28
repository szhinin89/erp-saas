import { describe, expect, it } from "vitest";
import { catalogRoutes } from "./catalogRoutes";

/**
 * PAYABLES-LEGACY-CLEANUP-13 — guard de regresión: el flujo legacy de CxP exclusivo de Compras
 * (`/finance/payables`, `AccountsPayablePage`) fue eliminado por completo en favor de la pantalla
 * genérica `/payables` (Compras + Gastos, vía la API `/api/v1/payables`). Si alguien reintrodujera
 * la ruta legacy, este test fallaría.
 *
 * NAVIGATION-MENU-CLEANUP-PAYABLES-EXPENSES-01 — extiende el guard a Pagos a Proveedores: debe
 * seguir siendo un módulo de rutas independiente (no fusionado con el flujo legacy de Compras
 * ni con `/finance/payables`), con exactamente una ruta por pantalla.
 */
describe("catalogRoutes — limpieza del flujo legacy de CxP", () => {
  const paths = catalogRoutes.map((route) => route.props.path);

  it("no existe la ruta legacy /finance/payables", () => {
    expect(paths).not.toContain("/finance/payables");
  });

  it("existe exactamente una pantalla de CxP genérica en /payables", () => {
    expect(paths.filter((p) => p === "/payables")).toHaveLength(1);
    expect(paths.filter((p) => p === "/payables/:id")).toHaveLength(1);
  });

  it("no existe ninguna ruta legacy /api/v1/purchase-payables", () => {
    expect(paths.some((p) => p?.includes("purchase-payables"))).toBe(false);
  });

  it("existe exactamente una pantalla de Pagos a proveedores por ruta", () => {
    expect(paths.filter((p) => p === "/supplier-payments")).toHaveLength(1);
    expect(paths.filter((p) => p === "/supplier-payments/new")).toHaveLength(1);
    expect(paths.filter((p) => p === "/supplier-payments/:id")).toHaveLength(1);
  });
});
