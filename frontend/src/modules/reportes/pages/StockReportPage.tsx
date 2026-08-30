import { useCallback, useEffect, useState } from "react";
import { useAuthStore } from "../../../store/authStore";
import {
  ReportPage,
  ReportKpiCard,
  ReportFiltersBar,
  ReportFilterField,
  ReportStatusBadge,
  type RptStatusTone,
} from "../../../components/ReportPageTemplate";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { formatMoney } from "../../../lib/sanitizers";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import {
  stockService,
  type StockReportRowDto,
  type StockReportStatus,
} from "../../inventory/stock/api/stockService";
import {
  warehouseService,
  type WarehouseDto,
} from "../../inventory/warehouses/api/warehouseService";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";

const STATUS_LABEL: Record<StockReportStatus, string> = {
  SinStock: "Sin stock",
  Bajo: "Bajo",
  Disponible: "Disponible",
};

const STATUS_TONE: Record<StockReportStatus, RptStatusTone> = {
  SinStock: "error",
  Bajo: "warning",
  Disponible: "success",
};

export function StockReportPage() {
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [warehouseId, setWarehouseId] = useState("");
  const [search, setSearch] = useState("");
  const [rows, setRows] = useState<StockReportRowDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    warehouseService
      .list("active")
      .then(setWarehouses)
      .catch(() => {
        // El selector de bodega degrada a "Todas" si el catálogo no carga; no bloquea el reporte.
      });
  }, [companySessionVersion]);

  const fetchReport = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const r = await stockService.getReport(
        warehouseId || undefined,
        search || undefined,
      );
      setRows(r);
    } catch (err: unknown) {
      const msg = formatApiRequestError(err, {
        generic: "No se pudo cargar el reporte de stock.",
      });
      setError(msg);
      message.error(msg);
    } finally {
      setLoading(false);
    }
  }, [warehouseId, search]);

  useEffect(() => {
    void fetchReport();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [companySessionVersion]);

  const totalValue = rows.reduce((sum, r) => sum + r.stockValue, 0);
  const totalQuantity = rows.reduce((sum, r) => sum + r.quantity, 0);

  const stockColumns: ZHDataTableColumn<StockReportRowDto>[] = [
    { key: "sku", header: "SKU", cellClassName: "subtle", render: (row) => row.sku },
    { key: "product", header: "Producto", render: (row) => row.productName },
    { key: "warehouse", header: "Bodega", cellClassName: "subtle", render: (row) => row.warehouseName },
    { key: "quantity", header: "Stock Actual", align: "right", render: (row) => formatMoney(row.quantity, 4) },
    { key: "available", header: "Disponible", align: "right", render: (row) => formatMoney(row.availableQuantity, 4) },
    { key: "avgCost", header: "Costo Promedio", align: "right", render: (row) => formatMoney(row.averageCost, 6) },
    { key: "stockValue", header: "Valor Inventario", align: "right", render: (row) => formatMoney(row.stockValue) },
    {
      key: "status",
      header: "Estado",
      render: (row) => <ReportStatusBadge label={STATUS_LABEL[row.status]} tone={STATUS_TONE[row.status]} />,
    },
  ];

  return (
    <ReportPage
      key={`stock-report-${companySessionVersion}`}
      breadcrumb={["ERP", "REPORTES"]}
      title="Reporte de Stock"
      subtitle="Stock actual por bodega."
    >
      <div className="pg-kpis">
        <ReportKpiCard
          icon="inventory_2"
          tone="primary"
          label="Productos"
          value={String(rows.length)}
        />
        <ReportKpiCard
          icon="functions"
          tone="secondary"
          label="Unidades Totales"
          value={formatMoney(totalQuantity, 4)}
        />
        <ReportKpiCard
          icon="payments"
          tone="tertiary"
          label="Valor de Inventario"
          value={formatMoney(totalValue)}
        />
      </div>

      <ReportFiltersBar
        onClear={() => {
          setWarehouseId("");
          setSearch("");
        }}
        onApply={() => void fetchReport()}
        clearLabel="Limpiar"
        applyLabel="Buscar"
      >
        <ReportFilterField label="Bodega" icon="warehouse">
          <ZhSelect
            className="zh-input"
            value={warehouseId}
            onChange={(e) => setWarehouseId(e.target.value)}
          >
            <option value="">Todas las bodegas</option>
            {warehouses.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </ZhSelect>
        </ReportFilterField>
        <ReportFilterField label="Producto / SKU" icon="search">
          <ZhTextInput
            className="zh-input"
            placeholder="Buscar por producto o SKU…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </ReportFilterField>
      </ReportFiltersBar>

      {error && <ZHPageNotice variant="error" message="Error" detail={error} />}

      <div className="pg-section">
        <ZHDataTable
          columns={stockColumns}
          rows={rows}
          rowKey={(row) => `${row.productId}-${row.warehouseId}`}
          loading={loading}
          showRowNumber
          emptyMessage="No hay stock registrado para los filtros seleccionados."
        />
      </div>
    </ReportPage>
  );
}
