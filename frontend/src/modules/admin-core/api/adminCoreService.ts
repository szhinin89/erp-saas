import { api } from "../../lib/api";
import type { ApiResponse } from "../../../types/api";
import type {
  AdminCoreCompany,
  AdminCoreCompanyAdministrator,
  AdminCoreRegeneratedPassword,
  AdminCoreTenant,
} from "../../../types/adminCore";

async function fetchCompanies(): Promise<AdminCoreCompany[]> {
  const { data } = await api.get<ApiResponse<AdminCoreCompany[]>>(
    "/api/v1/admin-core/companies",
  );
  return data.data ?? [];
}

/**
 * No existe un endpoint dedicado de tenants — se derivan de las empresas ya devueltas por
 * GET /api/v1/admin-core/companies (mismo dato que agrupa el dashboard global). Un tenant sin
 * ninguna empresa todavía no aparece aquí; no hay flujo de alta de tenants en AdminCore hoy.
 */
function deriveTenants(companies: AdminCoreCompany[]): AdminCoreTenant[] {
  const byId = new Map<string, AdminCoreTenant>();
  for (const c of companies) {
    if (!byId.has(c.tenantId)) {
      byId.set(c.tenantId, {
        tenantId: c.tenantId,
        tenantName: c.tenantName,
        tenantIsActive: c.tenantIsActive,
      });
    }
  }
  return Array.from(byId.values()).sort((a, b) =>
    a.tenantName.localeCompare(b.tenantName),
  );
}

export const adminCoreService = {
  listCompanies: fetchCompanies,

  async updateCompany(company: AdminCoreCompany, taxId: string): Promise<void> {
    await api.put(`/api/v1/admin-core/companies/${company.companyId}`, {
      id: company.companyId,
      legalName: company.legalName,
      tradeName: company.tradeName,
      isActive: company.isActive,
      taxId: taxId.trim() || null,
    });
  },

  async listCompanyAdministrators(
    companyId: string,
  ): Promise<AdminCoreCompanyAdministrator[]> {
    const { data } = await api.get<ApiResponse<AdminCoreCompanyAdministrator[]>>(
      `/api/v1/admin-core/companies/${companyId}/administrators`,
    );
    return data.data ?? [];
  },

  /**
   * Genera una contraseña temporal nueva (invalida la anterior, revoca sus sesiones y exige
   * cambio en el próximo ingreso). La respuesta es la única vez que se muestra.
   */
  async regenerateAdministratorPassword(
    companyId: string,
    identityUserId: string,
  ): Promise<AdminCoreRegeneratedPassword> {
    const { data } = await api.post<ApiResponse<AdminCoreRegeneratedPassword>>(
      `/api/v1/admin-core/companies/${companyId}/administrators/${identityUserId}/temporary-password`,
    );
    return data.data;
  },

  async listTenants(): Promise<AdminCoreTenant[]> {
    return deriveTenants(await fetchCompanies());
  },
};
