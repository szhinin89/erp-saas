import { api } from "../../lib/api";
import type { ApiResponse } from "../../../types/api";

/**
 * ADMIN-PERMISSIONS-SSOT-KERNEL-02 / NAV-HIERARCHY-UNIFY-01 — tipos espejo de
 * PermissionCatalogDto/GroupDto/CategoryDto/ItemDto/ActionDto (backend). El catálogo se deriva
 * 100% de KernelRegistry — nunca de un catálogo paralelo en frontend. Jerarquía de 4 niveles:
 * Grupo (módulo) → Categoría → Pantalla → Acciones — misma agrupación que ya usa el menú lateral
 * (`GET /api/v1/me/menu`), derivada de los mismos `[NavItem]` contenedor.
 */
export type PermissionCatalogAction = {
  code: string;
  label: string;
  description: string;
  sortOrder: number;
};

export type PermissionCatalogItem = {
  id: string;
  labelKey: string;
  route: string;
  permission: string;
  sortOrder: number;
  actions: PermissionCatalogAction[];
};

export type PermissionCatalogCategory = {
  id: string;
  labelKey: string;
  sortOrder: number;
  items: PermissionCatalogItem[];
};

export type PermissionCatalogGroup = {
  code: string;
  labelKey: string;
  sortOrder: number;
  categories: PermissionCatalogCategory[];
};

export type PermissionCatalog = {
  groups: PermissionCatalogGroup[];
};

export const adminPermissionsService = {
  getCatalog: () =>
    api
      .get<ApiResponse<PermissionCatalog>>("/api/v1/admin/permissions/catalog")
      .then((r) => r.data.data),
};
