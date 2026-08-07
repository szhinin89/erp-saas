import { useCallback, useEffect, useState } from "react";
import { useAuthStore } from "../../../store/authStore";
import {
  ReportPage,
  ReportKpiCard,
  ReportFiltersBar,
  ReportFilterField,
  ReportTablePanel,
  ReportTable,
  ReportTableHead,
  ReportTh,
  ReportTd,
  ReportStatusBadge,
  type RptStatusTone,
} from "../../../components/ReportPageTemplate";
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

      <ReportTablePanel total={rows.length} showing={rows.length > 0 ? `1-${rows.length}` : "0"} hasNext={false}>
        {loading ? (
          <p className="rpt-footer-note">Cargando…</p>
        ) : error ? (
          <p className="rpt-footer-note">{error}</p>
        ) : rows.length === 0 ? (
          <p className="rpt-footer-note">
            No hay stock registrado para los filtros seleccionados.
          </p>
        ) : (
          <ReportTable>
            <ReportTableHead>
              <ReportTh>SKU</ReportTh>
              <ReportTh>Producto</ReportTh>
              <ReportTh>Bodega</ReportTh>
              <ReportTh align="right">Stock Actual</ReportTh>
              <ReportTh align="right">Disponible</ReportTh>
              <ReportTh align="right">Costo Promedio</ReportTh>
              <ReportTh align="right">Valor Inventario</ReportTh>
              <ReportTh>Estado</ReportTh>
            </ReportTableHead>
            <tbody>
              {rows.map((row) => (
                <tr key={`${row.productId}-${row.warehouseId}`}>
                  <ReportTd className="subtle">{row.sku}</ReportTd>
                  <ReportTd>{row.productName}</ReportTd>
                  <ReportTd className="subtle">{row.warehouseName}</ReportTd>
                  <ReportTd align="right">{formatMoney(row.quantity, 4)}</ReportTd>
                  <ReportTd align="right">
                    {formatMoney(row.availableQuantity, 4)}
                  </ReportTd>
                  <ReportTd align="right">{formatMoney(row.averageCost, 6)}</ReportTd>
                  <ReportTd align="right">{formatMoney(row.stockValue)}</ReportTd>
                  <ReportTd>
                    <ReportStatusBadge
                      label={STATUS_LABEL[row.status]}
                      tone={STATUS_TONE[row.status]}
                    />
                  </ReportTd>
                </tr>
              ))}
            </tbody>
          </ReportTable>
        )}
      </ReportTablePanel>
    </ReportPage>
  );
}
