import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useSessionStore } from "../../../store/sessionStore";
import type { CompanySettingsTabId } from "./companySettingsTabs";

/**
 * NAV-COVERAGE-H2 — permiso de lectura que exige la API de cada pestaña de Datos de la empresa.
 * `erp.companies.view` y `settings.company.view` son capacidades independientes: ninguna implica
 * la otra. Ventas lee los defaults de factura de OrgConfig (`settings.company.view`); el resto lee
 * el perfil de la empresa activa (`erp.companies.view`). Decimales usa una API solo autenticada y
 * conserva su gate previo. La edición sigue en cada sección (`erp.companies.update`).
 */
export const COMPANY_SETTINGS_TAB_VIEW_PERMISSION: Record<CompanySettingsTabId, string> = {
  profile: "erp.companies.view",
  branding: "erp.companies.view",
  fiscal: "erp.companies.view",
  operation: "erp.companies.view",
  documents: "erp.companies.view",
  decimals: "erp.companies.view",
  sales: "settings.company.view",
  readiness: "erp.companies.view",
};

export const COMPANY_SETTINGS_PATH = "/settings/company";

export type CompanySettingsTabAccess =
  | { status: "loading" }
  | { status: "ready"; canViewTab: (id: CompanySettingsTabId) => boolean };

/**
 * Acceso por pestaña, sin el fallback permisivo de `canShow`: mientras la sesión no se resuelve
 * no se concede ninguna pestaña, y una lista de permisos vacía no autoriza nada. El backend sigue
 * siendo la autoridad final ([Authorize(Policy="perm:*")]).
 */
export function useCompanySettingsTabAccess(): CompanySettingsTabAccess {
  const { isAdminRole } = usePermissionsUi();
  const isLoaded = useSessionStore((s) => s.isLoaded);
  const permissions = useSessionStore((s) => s.authorization?.permissions);

  if (isAdminRole) return { status: "ready", canViewTab: () => true };
  if (!isLoaded) return { status: "loading" };

  const granted = new Set(permissions ?? []);
  return {
    status: "ready",
    canViewTab: (id) =>
      granted.has("*") || granted.has(COMPANY_SETTINGS_TAB_VIEW_PERMISSION[id]),
  };
}

/** Pestaña destino de una ruta `/settings/company?tab=…`; null si la ruta no es de este hub. */
export function companySettingsTabOf(route: string): string | null {
  const [path, query = ""] = route.split("#")[0].split("?");
  if (path !== COMPANY_SETTINGS_PATH) return null;
  return new URLSearchParams(query).get("tab") ?? "";
}
