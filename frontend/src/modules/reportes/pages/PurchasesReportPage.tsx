import { useCallback, useEffect, useState } from "react";
import { useAuthStore } from "../../../store/authStore";
import {
  ReportPage,
  ReportKpiCard,
  ReportFiltersBar,
  ReportFilterField,
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
import {
  purchaseService,
  type PurchasesReportRowDto,
  type PurchasesReportTotalsDto,
} from "../../purchases/api/purchaseService";
import { SupplierPicker } from "../../purchases/components/SupplierPicker";
import { ZhDateInput } from "../../../components/zh/inputs";
import type { SupplierPickerRow } from "../../masterData/types/businessPartner.types";

const STATUS_LABEL: Record<string, string> = {
  Draft: "Borrador",
  Confirmed: "Confirmada",
  Cancelled: "Anulada",
};

const STATUS_TONE: Record<string, RptStatusTone> = {
  Draft: "warning",
  Confirmed: "success",
  Cancelled: "error",
};

const EMPTY_TOTALS: PurchasesReportTotalsDto = {
  count: 0,
  subtotal: 0,
  totalVat: 0,
  totalDiscount: 0,
  grandTotal: 0,
};

export function PurchasesReportPage() {
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);
  const [dateFrom, setDateFrom] = useState(todayIso());
  const [dateTo, setDateTo] = useState(todayIso());
  const [supplier, setSupplier] = useState<SupplierPickerRow | null>(null);
  const [rows, setRows] = useState<PurchasesReportRowDto[]>([]);
  const [totals, setTotals] = useState<PurchasesReportTotalsDto>(EMPTY_TOTALS);
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
      const r = await purchaseService.supplierReport(
        dateFrom,
        dateTo,
        supplier?.id,
      );
      setRows(r.items);
      setTotals(r.totals);
    } catch (err: unknown) {
      const msg = formatApiRequestError(err, {
        generic: "No se pudo cargar el reporte de compras.",
      });
      setError(msg);
      message.error(msg);
    } finally {
      setLoading(false);
    }
  }, [dateFrom, dateTo, supplier]);

  useEffect(() => {
    void fetchReport();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [companySessionVersion]);

  const purchasesReportColumns: ZHDataTableColumn<PurchasesReportRowDto>[] = [
    { key: "document", header: "Documento", render: (row) => <ReportRowId id={row.invoiceNumber} /> },
    { key: "date", header: "Fecha", cellClassName: "subtle", render: (row) => formatDate(row.issueDate) },
    { key: "supplier", header: "Proveedor", render: (row) => row.supplierName },
    { key: "taxId", header: "RUC/ID", cellClassName: "subtle", render: (row) => row.supplierTaxId },
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
  ];

  return (
    <ReportPage
      key={`purchases-report-${companySessionVersion}`}
      breadcrumb={["ERP", "REPORTES"]}
      title="Reporte de Compras"
      subtitle="Compras por proveedor — resumen y detalle de facturas."
    >
      <div className="pg-kpis">
        <ReportKpiCard
          icon="payments"
          tone="primary"
          label="Total Comprado"
          value={formatMoney(totals.grandTotal)}
        />
        <ReportKpiCard
          icon="receipt_long"
          tone="secondary"
          label="N.º de Compras"
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
          setSupplier(null);
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
        <ReportFilterField label="Proveedor">
          <SupplierPicker
            value={supplier?.id ?? null}
            onChange={setSupplier}
          />
        </ReportFilterField>
      </ReportFiltersBar>

      {error && <ZHPageNotice variant="error" message="Error" detail={error} />}

      <div className="pg-section">
        <ZHDataTable
          columns={purchasesReportColumns}
          rows={rows}
          rowKey={(row) => row.id}
          loading={loading}
          showRowNumber
          emptyMessage="No hay compras en el rango seleccionado."
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
