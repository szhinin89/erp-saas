import { useCallback, useEffect, useState } from "react";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import { ZhDateInput, ZhSelect, ZhTextInput } from "../../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../../components/zh/ZHMoneyValue";
import { formatDate } from "../../../../lib/formatters/dateFormatters";
import { message } from "../../../../lib/messages";
import { formatApiRequestError } from "../../../lib/apiError";
import { accountingApi, type GeneralJournalLineDto } from "../../api/accountingApi";

const PAGE_SIZE = 50;

const SOURCE_MODULE_OPTIONS = [
  { value: "", label: "Todos los orígenes" },
  { value: "Sales", label: "Ventas" },
  { value: "Purchases", label: "Compras" },
  { value: "Finance", label: "Caja / Cobros / Pagos" },
  { value: "Inventory", label: "Inventario" },
  { value: "Accounting", label: "Contabilidad (reversos)" },
];

function firstDayOfMonth(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * Libro Diario (ACCOUNTING-REPORTS-09) — todas las líneas de asientos Posted en el rango,
 * consume `GET /accounting/reports/general-journal`. Solo lectura, sin recálculo.
 * ACCOUNTING-REPORTS-DS-QA-FIX-10E: filtros movidos de `ZHCard actions` (slot de 2-3 campos) a
 * `ZHFilterBar` (barra dedicada, campos con `ZHField` label visible, `zh-filterbar__field` con
 * min-width responsive); totales con `ZHMoneyValue`; `pg-flex`/`pg-gap-8` (clases sin CSS
 * definido en ningún stylesheet del proyecto — layout roto silenciosamente) reemplazadas por
 * `zh-form-actions-row`/`zh-actions`, ambas reales en `zh-ui.css`.
 */
export function GeneralJournalReportTab() {
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(today());
  const [sourceModule, setSourceModule] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [lines, setLines] = useState<GeneralJournalLineDto[]>([]);
  const [totalDebit, setTotalDebit] = useState(0);
  const [totalCredit, setTotalCredit] = useState(0);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);

  const fetchReport = useCallback(async () => {
    setLoading(true);
    try {
      const r = await accountingApi.getGeneralJournalReport({
        fromDate,
        toDate,
        sourceModule: sourceModule || undefined,
        search: search || undefined,
        pageNumber: page,
        pageSize: PAGE_SIZE,
      });
      setLines(r.lines);
      setTotalDebit(r.totalDebit);
      setTotalCredit(r.totalCredit);
      setTotalCount(r.totalCount);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo cargar el Libro Diario." }),
      );
    } finally {
      setLoading(false);
    }
  }, [fromDate, toDate, sourceModule, search, page]);

  useEffect(() => {
    void fetchReport();
  }, [fetchReport]);

  const columns: ZHDataTableColumn<GeneralJournalLineDto>[] = [
    { key: "entryDate", header: "Fecha", render: (r) => formatDate(r.entryDate) },
    { key: "entryNumber", header: "Asiento", render: (r) => r.entryNumber ?? "—" },
    {
      key: "account",
      header: "Cuenta",
      render: (r) => (
        <>
          <code className="prd-sku">{r.accountCode}</code> {r.accountName}
        </>
      ),
    },
    { key: "description", header: "Descripción", render: (r) => r.description },
    {
      key: "source",
      header: "Documento origen",
      render: (r) => r.sourceDocumentNumber ?? `${r.sourceModule} / ${r.sourceEventType}`,
    },
    {
      key: "debit",
      header: "Debe",
      align: "right",
      render: (r) => <ZHMoneyValue value={r.debit > 0 ? r.debit : null} />,
    },
    {
      key: "credit",
      header: "Haber",
      align: "right",
      render: (r) => <ZHMoneyValue value={r.credit > 0 ? r.credit : null} />,
    },
  ];

  return (
    <ZHCard title="Libro Diario">
      <ZHFilterBar disabled={loading}>
        <div className="zh-filterbar__field">
          <ZHField label="Desde" density="compact">
            <ZhDateInput
              className="zh-input"
              value={fromDate}
              onChange={(e) => { setPage(1); setFromDate(e.target.value); }}
            />
          </ZHField>
        </div>
        <div className="zh-filterbar__field">
          <ZHField label="Hasta" density="compact">
            <ZhDateInput
              className="zh-input"
              value={toDate}
              onChange={(e) => { setPage(1); setToDate(e.target.value); }}
            />
          </ZHField>
        </div>
        <div className="zh-filterbar__field">
          <ZHField label="Origen" density="compact">
            <ZhSelect
              className="zh-input"
              value={sourceModule}
              onChange={(e) => { setPage(1); setSourceModule(e.target.value); }}
            >
              {SOURCE_MODULE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </ZhSelect>
          </ZHField>
        </div>
        <div className="zh-filterbar__field zh-filterbar__field--grow">
          <ZHField label="Buscar" density="compact">
            <ZhTextInput
              className="zh-input"
              placeholder="N° de asiento o descripción"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              onKeyDown={(e) => { if (e.key === "Enter") { setPage(1); void fetchReport(); } }}
            />
          </ZHField>
        </div>
        <ZHBtn
          type="button"
          variant="primary"
          onClick={() => { setPage(1); void fetchReport(); }}
          disabled={loading}
        >
          Buscar
        </ZHBtn>
      </ZHFilterBar>

      {totalDebit !== totalCredit && lines.length > 0 && (
        <ZHPageNotice
          variant="warning"
          message="El total Debe no coincide con el total Haber en esta página del reporte."
        />
      )}
      <ZHDataTable
        columns={columns}
        rows={lines}
        rowKey={(r) => `${r.journalEntryId}-${r.accountId}-${r.debit}-${r.credit}`}
        loading={loading}
        showRowNumber
        rowNumberOffset={(page - 1) * PAGE_SIZE}
        emptyMessage="No hay asientos contabilizados (Posted) en el rango seleccionado. Ajuste las fechas o el filtro de origen."
        page={page}
        pageSize={PAGE_SIZE}
        onPageChange={setPage}
        total={totalCount}
      />
      {lines.length > 0 && (
        <div className="zh-actions">
          <span>Total Debe <ZHMoneyValue value={totalDebit} emphasis="total" /></span>
          <span>Total Haber <ZHMoneyValue value={totalCredit} emphasis="total" /></span>
        </div>
      )}
    </ZHCard>
  );
}
