import type { ReadinessActionTarget } from "../../../types/companyProfile";

/**
 * COMPANY-OPERATING-SETUP-01 — único lugar que traduce el ActionTarget semántico devuelto por el
 * backend a una ruta concreta de React Router. El backend nunca emite rutas de SPA (ver
 * ReadinessActionTarget en types/companyProfile.ts) — si esta tabla no cubre un target, el ítem
 * no debe intentar navegar (ver getOperationalReadinessActionRoute).
 */
const ACTION_TARGET_ROUTES: Record<ReadinessActionTarget, string> = {
  CompanyProfile: "/settings/company?tab=profile",
  CompanyBranding: "/settings/company?tab=branding",
  CompanyFiscalSettings: "/settings/company?tab=fiscal",
  CompanySalesSettings: "/settings/company?tab=sales",
  DecimalSettings: "/settings/company?tab=decimals",
  Branches: "/settings/branches",
  Establishments: "/settings/establishments",
  EmissionPoints: "/settings/emission-points",
  Warehouses: "/inventory/warehouses",
  CashRegisters: "/cash/registers",
  PriceLists: "/pricing",
  ElectronicInvoicingSettings: "/settings/electronic-invoicing",
  Items: "/inventory/items",
};

export function getOperationalReadinessActionRoute(
  target: ReadinessActionTarget | null,
): string | null {
  if (!target) return null;
  return ACTION_TARGET_ROUTES[target] ?? null;
}
