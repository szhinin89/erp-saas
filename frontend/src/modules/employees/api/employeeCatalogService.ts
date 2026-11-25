import { apiGet, apiPatch, apiPost } from "../../lib/apiEnvelope";
import type {
  EmployeeCatalogEntry,
  EmployeeCatalogKind,
  EmployeeCatalogOptions,
  PagedResult,
} from "../types/employee.types";

const base = (kind: EmployeeCatalogKind) => `/api/v1/employees/${kind}`;
const one = (kind: EmployeeCatalogKind, id: string) =>
  `${base(kind)}/${encodeURIComponent(id)}`;

/** Tamaño máximo de página que admite el backend. */
const MAX_TAKE = 200;

export const employeeCatalogService = {
  list: (
    kind: EmployeeCatalogKind,
    params: { isActive?: boolean; search?: string; skip?: number; take?: number },
    signal?: AbortSignal,
  ) => apiGet<PagedResult<EmployeeCatalogEntry>>(base(kind), { params, signal }),
  getById: (kind: EmployeeCatalogKind, id: string) =>
    apiGet<EmployeeCatalogEntry>(one(kind, id)),
  create: (kind: EmployeeCatalogKind, name: string) =>
    apiPost<EmployeeCatalogEntry>(base(kind), { name }),
  rename: (kind: EmployeeCatalogKind, id: string, rowVersion: number, name: string) =>
    apiPatch<EmployeeCatalogEntry>(one(kind, id), { rowVersion, name }),
  setActive: (kind: EmployeeCatalogKind, id: string, rowVersion: number, active: boolean) =>
    apiPost<EmployeeCatalogEntry>(`${one(kind, id)}/${active ? "activate" : "deactivate"}`, {
      rowVersion,
    }),

  /** Catálogo completo (activos e inactivos) para selectores: recorre todas las páginas. */
  async listAll(kind: EmployeeCatalogKind): Promise<EmployeeCatalogEntry[]> {
    const all: EmployeeCatalogEntry[] = [];
    for (;;) {
      const page = await employeeCatalogService.list(kind, { skip: all.length, take: MAX_TAKE });
      all.push(...page.items);
      if (page.items.length === 0 || all.length >= page.totalCount) return all;
    }
  },

  /** Los tres catálogos; exige `employees.catalogs.view`. */
  async loadOptions(): Promise<EmployeeCatalogOptions> {
    const [jobPositions, professions, specialties] = await Promise.all([
      employeeCatalogService.listAll("job-positions"),
      employeeCatalogService.listAll("professions"),
      employeeCatalogService.listAll("specialties"),
    ]);
    return { jobPositions, professions, specialties };
  },
};
