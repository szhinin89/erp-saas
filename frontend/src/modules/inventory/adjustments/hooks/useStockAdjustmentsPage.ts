import { useCallback, useEffect, useMemo, useState } from "react";
import { useI18n } from "../../../../i18n/i18n";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { readApiErrorMessage } from "../../../lib/apiError";
import { warehouseService } from "../../warehouses/api/warehouseService";
import type { WarehouseDto } from "../../warehouses/api/warehouseService";
import { inventoryAdjustmentReasonsService } from "../../adjustmentReasons/api/inventoryAdjustmentReasonsService";
import type { InventoryAdjustmentReasonDto } from "../../adjustmentReasons/types";
import { stockAdjustmentsService } from "../api/stockAdjustmentsService";
import type {
  StockAdjustmentDto,
  StockAdjustmentListFilters,
} from "../types";
import { useAdjustmentLifecycleActions } from "./useAdjustmentLifecycleActions";

const PAGE_SIZE = 20;

export type StockAdjustmentsFilterState = {
  warehouseId: string;
  status: string;
  reasonId: string;
  movementType: string;
  startDate: string;
  endDate: string;
};

const EMPTY_FILTERS: StockAdjustmentsFilterState = {
  warehouseId: "",
  status: "",
  reasonId: "",
  movementType: "",
  startDate: "",
  endDate: "",
};

/**
 * INVENTORY-ADJUSTMENTS-03 — lista de ajustes de inventario. Los filtros son exactamente los que
 * soporta `GET /inventory/stock/adjustments` (warehouseId/status/reasonId/movementType/startDate/
 * endDate + paginación) — no se ofrece filtro por producto porque el backend no lo expone y una
 * UI que promete un filtro inexistente es peor que no tenerlo.
 *
 * Toda acción se habilita por estado Y permiso: el estado dice qué transición es válida en el
 * dominio, el permiso dice si este usuario puede ejecutarla. `canShow` nunca sustituye la
 * autorización real — el backend vuelve a validar el permiso en cada endpoint.
 */
export function useStockAdjustmentsPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();

  const canView = canShow("inventory.adjustments.view");
  const canCreate = canShow("inventory.adjustments.create");
  const canUpdate = canShow("inventory.adjustments.update");
  const canExecute = canShow("inventory.adjustments.confirm");
  const canCancel = canShow("inventory.adjustments.cancel");

  const [rows, setRows] = useState<StockAdjustmentDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageNumber, setPageNumber] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filters, setFilters] = useState<StockAdjustmentsFilterState>(EMPTY_FILTERS);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [reasons, setReasons] = useState<InventoryAdjustmentReasonDto[]>([]);

  const fetchList = useCallback(async () => {
    setError(null);
    setLoading(true);
    try {
      const query: StockAdjustmentListFilters = {
        warehouseId: filters.warehouseId || undefined,
        status: filters.status || undefined,
        reasonId: filters.reasonId || undefined,
        movementType: filters.movementType || undefined,
        startDate: filters.startDate || undefined,
        endDate: filters.endDate || undefined,
        pageNumber,
        pageSize: PAGE_SIZE,
      };
      const result = await stockAdjustmentsService.list(query);
      setRows(result?.items ?? []);
      setTotalCount(result?.totalCount ?? 0);
    } catch (err) {
      setRows([]);
      setTotalCount(0);
      setError(
        readApiErrorMessage(err) ??
          t(
            "inventory.adjustments.messages.listError",
            "No se pudieron cargar los ajustes de inventario.",
          ),
      );
    } finally {
      setLoading(false);
    }
  }, [filters, pageNumber, t]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  useEffect(() => {
    warehouseService
      .list("active")
      .then((list) => setWarehouses(list ?? []))
      .catch(() => setWarehouses([]));
    inventoryAdjustmentReasonsService
      .list(true)
      .then((list) => setReasons(list ?? []))
      .catch(() => setReasons([]));
  }, []);

  const lifecycle = useAdjustmentLifecycleActions(
    useCallback(() => {
      void fetchList();
    }, [fetchList]),
  );

  const setFilter = useCallback(
    (key: keyof StockAdjustmentsFilterState, value: string) => {
      setPageNumber(1);
      setFilters((prev) => ({ ...prev, [key]: value }));
    },
    [],
  );

  const clearFilters = useCallback(() => {
    setPageNumber(1);
    setFilters(EMPTY_FILTERS);
  }, []);

  const hasFilters = useMemo(
    () => Object.values(filters).some((v) => v !== ""),
    [filters],
  );

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return {
    t,
    canView,
    canCreate,
    canUpdate,
    canExecute,
    canCancel,
    rows,
    totalCount,
    pageNumber,
    setPageNumber,
    totalPages,
    loading,
    error,
    filters,
    setFilter,
    clearFilters,
    hasFilters,
    warehouses,
    reasons,
    lifecycle,
    fetchList,
  };
}
