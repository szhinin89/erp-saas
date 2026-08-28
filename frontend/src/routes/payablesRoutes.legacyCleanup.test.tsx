import { describe, expect, it } from "vitest";
import { catalogRoutes } from "./catalogRoutes";

/**
 * PAYABLES-LEGACY-CLEANUP-13 — guard de regresión: el flujo legacy de CxP exclusivo de Compras
 * (`/finance/payables`, `AccountsPayablePage`) fue eliminado por completo en favor de la pantalla
 * genérica `/payables` (Compras + Gastos, vía la API `/api/v1/payables`). Si alguien reintrodujera
 * la ruta legacy, este test fallaría.
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
});
