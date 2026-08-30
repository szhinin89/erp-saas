import { useCallback, useEffect, useState } from "react";
import { useAuthStore } from "../../../store/authStore";
import {
  ReportPage,
  ReportKpiCard,
  ReportFiltersBar,
  ReportFilterField,
  ReportClientAvatar,
  ReportRowId,
  ReportStatusBadge,
  type RptStatusTone,
} from "../../../components/ReportPageTemplate";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { formatMoney } from "../../../lib/sanitizers";
import { formatDate, todayIso } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { ZhDateInput } from "../../../components/zh/inputs";
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

  const salesReportColumns: ZHDataTableColumn<SalesReportRowDto>[] = [
    { key: "invoice", header: "Factura", render: (row) => <ReportRowId id={row.invoiceNumber} /> },
    { key: "date", header: "Fecha", cellClassName: "subtle", render: (row) => formatDate(row.issueDate) },
    { key: "customer", header: "Cliente", render: (row) => <ReportClientAvatar name={row.customerName} /> },
    { key: "subtotal", header: "Subtotal", align: "right", render: (row) => formatMoney(row.subtotal) },
    { key: "vat", header: "IVA", align: "right", render: (row) => formatMoney(row.totalVat) },
    { key: "discount", header: "Descuento", align: "right", render: (row) => formatMoney(row.totalDiscount) },
    { key: "total", header: "Total", align: "right", render: (row) => formatMoney(row.grandTotal) },
    {
      key: "status",
      header: "Estado",
      render: (row) => (
        <ReportStatusBadge label={STATUS_LABEL[row.status] ?? row.status} tone={STATUS_TONE[row.status] ?? "neutral"} />
      ),
    },
    {
      key: "emission",
      header: "Emisión",
      cellClassName: "subtle",
      render: (row) => EMISSION_LABEL[row.emissionType] ?? row.emissionType,
    },
  ];

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
          <ZhDateInput
            className="zh-input"
            value={dateFrom}
            onChange={(e) => setDateFrom(e.target.value)}
          />
        </ReportFilterField>
        <ReportFilterField label="Hasta" icon="calendar_today">
          <ZhDateInput
            className="zh-input"
            value={dateTo}
            onChange={(e) => setDateTo(e.target.value)}
          />
        </ReportFilterField>
      </ReportFiltersBar>

      {error && <ZHPageNotice variant="error" message="Error" detail={error} />}

      <div className="pg-section">
        <ZHDataTable
          columns={salesReportColumns}
          rows={rows}
          rowKey={(row) => row.id}
          loading={loading}
          showRowNumber
          emptyMessage="No hay ventas en el rango seleccionado."
        />
        {!loading && rows.length > 0 && (
          <p className="rpt-footer-note zh-mt-8">
            Descuento total: {formatMoney(totals.totalDiscount)}
          </p>
        )}
      </div>
    </ReportPage>
  );
}
