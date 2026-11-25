import { useCallback, useEffect, useState } from "react";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useAsync } from "../../../hooks/useAsync";
import { useDebounce } from "../../../hooks/useDebounce";
import { useAuthStore } from "../../../store/authStore";
import { todayIso } from "../../../lib/formatters/dateFormatters";
import { branchLookupFacade } from "../../branches/facades/branchLookupFacade";
import { employeeErrorMessage, employeeService } from "../api/employeeService";
import { employeeCatalogService } from "../api/employeeCatalogService";
import type { EmployeeListItem } from "../types/employee.types";

export const EMPLOYEES_PAGE_SIZE = 25;

export type EmployeeStatusFilter = "" | "active" | "inactive";

export type EmployeeFilters = {
  search: string;
  status: EmployeeStatusFilter;
  jobPositionId: string;
  professionId: string;
  specialtyId: string;
  branchId: string;
  asOf: string;
};

const emptyFilters = (): EmployeeFilters => ({
  search: "",
  status: "",
  jobPositionId: "",
  professionId: "",
  specialtyId: "",
  branchId: "",
  asOf: todayIso(),
});

export function useEmployeesPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("employees.view");
  const canCreate = canShow("employees.create");
  // Cada selector usa su fuente oficial: sin su permiso no se consulta.
  const canViewCatalogs = canShow("employees.catalogs.view");
  const canViewBranches = canShow("settings.branches.view");
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);

  const [filters, setFilters] = useState<EmployeeFilters>(emptyFilters);
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<EmployeeListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(canView);
  const [error, setError] = useState("");
  const [tick, setTick] = useState(0);
  const search = useDebounce(filters.search, 300);

  const catalogs = useAsync(employeeCatalogService.loadOptions, canView && canViewCatalogs);
  const branches = useAsync(() => branchLookupFacade.list("all"), canView && canViewBranches);

  const { status, jobPositionId, professionId, specialtyId, branchId, asOf } = filters;

  useEffect(() => {
    if (!canView) return;
    const controller = new AbortController();
    setLoading(true);
    setError("");
    employeeService
      .list(
        {
          search: search.trim() || undefined,
          isActive: status === "" ? undefined : status === "active",
          jobPositionId: jobPositionId || undefined,
          professionId: professionId || undefined,
          specialtyId: specialtyId || undefined,
          branchId: branchId || undefined,
          // La fecha solo tiene sentido con sucursal; vacía = hoy de la empresa (backend).
          asOf: branchId && asOf ? asOf : undefined,
          skip: (page - 1) * EMPLOYEES_PAGE_SIZE,
          take: EMPLOYEES_PAGE_SIZE,
        },
        controller.signal,
      )
      .then((result) => {
        if (controller.signal.aborted) return;
        setItems(result.items);
        setTotalCount(result.totalCount);
        setLoading(false);
      })
      .catch((err) => {
        if (controller.signal.aborted) return;
        setItems([]);
        setTotalCount(0);
        setError(employeeErrorMessage(err, t("employees.list.loadError", "No se pudo cargar la lista de empleados.")));
        setLoading(false);
      });
    return () => controller.abort();
  }, [canView, search, status, jobPositionId, professionId, specialtyId, branchId, asOf, page, tick, companySessionVersion, t]);

  const setFilter = useCallback(<K extends keyof EmployeeFilters>(key: K, value: EmployeeFilters[K]) => {
    setFilters((prev) => ({ ...prev, [key]: value }));
    setPage(1);
  }, []);
  const clearFilters = useCallback(() => {
    setFilters(emptyFilters());
    setPage(1);
  }, []);
  const refetch = useCallback(() => setTick((v) => v + 1), []);

  return {
    canView,
    canCreate,
    canViewCatalogs,
    canViewBranches,
    filters,
    setFilter,
    clearFilters,
    page,
    setPage,
    items,
    totalCount,
    loading,
    error,
    refetch,
    catalogs,
    branches,
  };
}
