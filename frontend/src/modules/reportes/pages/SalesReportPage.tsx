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
  ReportClientAvatar,
  ReportRowId,
  ReportStatusBadge,
  type RptStatusTone,
} from "../../../components/ReportPageTemplate";
import { formatMoney } from "../../../lib/sanitizers";
import { formatDate, todayIso } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import {
  salesService,
  type SalesReportRowDto,
  type SalesReportTotalsDto,
} from "../../sales/api/salesService";

const STATUS_LABEL: Record<string, string> = {
  Draft: "Borrador",
  Authorized: "Autorizada",
  Cancelled: "Anulada",
};

const STATUS_TONE: Record<string, RptStatusTone> = {
  Draft: "warning",
  Authorized: "success",
  Cancelled: "error",
};

const EMISSION_LABEL: Record<string, string> = {
  Electronic: "Electrónica",
  Physical: "Física",
};

const EMPTY_TOTALS: SalesReportTotalsDto = {
  count: 0,
  subtotal: 0,
  totalVat: 0,
  totalDiscount: 0,
  grandTotal: 0,
};

export function SalesReportPage() {
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);
  const [dateFrom, setDateFrom] = useState(todayIso());
  const [dateTo, setDateTo] = useState(todayIso());
  const [rows, setRows] = useState<SalesReportRowDto[]>([]);
  const [totals, setTotals] = useState<SalesReportTotalsDto>(EMPTY_TOTALS);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchReport = useCallback(async () => {
    if (dateFrom > dateTo) {
      message.warning("La fecha 'desde' no puede ser posterior a la fecha 'hasta'.");
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const r = await salesService.dailyReport(dateFrom, dateTo);
      setRows(r.items);
      setTotals(r.totals);
    } catch (err: unknown) {
      const msg = formatApiRequestError(err, {
        generic: "No se pudo cargar el reporte de ventas.",
      });
      setError(msg);
      message.error(msg);
    } finally {
      setLoading(false);
    }
  }, [dateFrom, dateTo]);

  useEffect(() => {
    void fetchReport();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [companySessionVersion]);

  return (
    <ReportPage
      key={`sales-report-${companySessionVersion}`}
      breadcrumb={["ERP", "REPORTES"]}
      title="Reporte de Ventas"
      subtitle="Ventas del día — resumen y detalle de facturas."
    >
      <div className="pg-kpis">
        <ReportKpiCard
          icon="payments"
          tone="primary"
          label="Total Vendido"
          value={formatMoney(totals.grandTotal)}
        />
        <ReportKpiCard
          icon="receipt_long"
          tone="secondary"
          label="N.º de Ventas"
          value={String(totals.count)}
        />
        <ReportKpiCard
          icon="calculate"
          tone="tertiary"
          label="Subtotal"
          value={formatMoney(totals.subtotal)}
        />
        <ReportKpiCard
          icon="percent"
          tone="tertiary"
          label="IVA"
          value={formatMoney(totals.totalVat)}
        />
      </div>

      <ReportFiltersBar
        onClear={() => {
          setDateFrom(todayIso());
          setDateTo(todayIso());
        }}
        onApply={() => void fetchReport()}
        clearLabel="Hoy"
        applyLabel="Buscar"
      >
        <ReportFilterField label="Desde" icon="calendar_today">
          <input
            type="date"
            className="zh-input"
            value={dateFrom}
            onChange={(e) => setDateFrom(e.target.value)}
          />
        </ReportFilterField>
        <ReportFilterField label="Hasta" icon="calendar_today">
          <input
            type="date"
            className="zh-input"
            value={dateTo}
            onChange={(e) => setDateTo(e.target.value)}
          />
        </ReportFilterField>
      </ReportFiltersBar>

      <ReportTablePanel
        total={totals.count}
        showing={rows.length > 0 ? `1-${rows.length}` : "0"}
        hasNext={false}
        footerLeft={`Descuento total: ${formatMoney(totals.totalDiscount)}`}
      >
        {loading ? (
          <p className="rpt-footer-note">Cargando…</p>
        ) : error ? (
          <p className="rpt-footer-note">{error}</p>
        ) : rows.length === 0 ? (
          <p className="rpt-footer-note">
            No hay ventas en el rango seleccionado.
          </p>
        ) : (
          <ReportTable>
            <ReportTableHead>
              <ReportTh>Factura</ReportTh>
              <ReportTh>Fecha</ReportTh>
              <ReportTh>Cliente</ReportTh>
              <ReportTh align="right">Subtotal</ReportTh>
              <ReportTh align="right">IVA</ReportTh>
              <ReportTh align="right">Descuento</ReportTh>
              <ReportTh align="right">Total</ReportTh>
              <ReportTh>Estado</ReportTh>
              <ReportTh>Emisión</ReportTh>
            </ReportTableHead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id}>
                  <ReportTd>
                    <ReportRowId id={row.invoiceNumber} />
                  </ReportTd>
                  <ReportTd className="subtle">{formatDate(row.issueDate)}</ReportTd>
                  <ReportTd>
                    <ReportClientAvatar name={row.customerName} />
                  </ReportTd>
                  <ReportTd align="right">{formatMoney(row.subtotal)}</ReportTd>
                  <ReportTd align="right">{formatMoney(row.totalVat)}</ReportTd>
                  <ReportTd align="right">{formatMoney(row.totalDiscount)}</ReportTd>
                  <ReportTd align="right">{formatMoney(row.grandTotal)}</ReportTd>
                  <ReportTd>
                    <ReportStatusBadge
                      label={STATUS_LABEL[row.status] ?? row.status}
                      tone={STATUS_TONE[row.status] ?? "neutral"}
                    />
                  </ReportTd>
                  <ReportTd className="subtle">
                    {EMISSION_LABEL[row.emissionType] ?? row.emissionType}
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
