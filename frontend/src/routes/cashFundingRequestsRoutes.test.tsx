import { describe, expect, it } from "vitest";
import { catalogRoutes } from "./catalogRoutes";

/**
 * ZH-CASH-FUNDING-REQUEST-UI-FINAL-02E-EF — el ítem de menú "Caja > Solicitudes de efectivo"
 * (backend, TreasuryModule) apunta a /treasury/cash/funding-requests: la ruta existe (no queda vacía)
 * y hay exactamente una pantalla de bandeja y una de detalle.
 */
describe("catalogRoutes — solicitudes de efectivo", () => {
  const paths = catalogRoutes.map((route) => route.props.path);

  it("1. la ruta del menú y su detalle existen exactamente una vez", () => {
    expect(paths.filter((p) => p === "/treasury/cash/funding-requests")).toHaveLength(1);
    expect(paths.filter((p) => p === "/treasury/cash/funding-requests/:id")).toHaveLength(1);
  });
});
