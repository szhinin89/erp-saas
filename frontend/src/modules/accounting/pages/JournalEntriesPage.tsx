import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { formatMoney } from "../../../lib/sanitizers";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { accountingApi, type JournalEntryListItemDto } from "../api/accountingApi";
// ACCOUNTING-DS-FULL-AUDIT-10F: sin este import, `.prd-sku` (usado abajo para el código de
// documento origen truncado) no tiene estilo — mismo root cause ya corregido en
// AccountingReportsPage.tsx (ACCOUNTING-REPORTS-DS-QA-FIX-10E).
import "../../../styles/shared/items-catalog.css";

const PAGE_SIZE = 20;

const STATUS_OPTIONS = [
  { value: "", label: "Todos los estados" },
  { value: "Draft", label: "Borrador" },
  { value: "Posted", label: "Contabilizado" },
  { value: "Reversed", label: "Reversado" },
];

function statusBadge(status: string): { label: string; variant: "gray" | "green" | "red" } {
  switch (status) {
    case "Posted":
      return { label: "Contabilizado", variant: "green" };
    case "Reversed":
      return { label: "Reversado", variant: "red" };
    default:
      return { label: "Borrador", variant: "gray" };
  }
}

/**
 * Listado de asientos contables (ACCOUNTING-LEDGER-VISIBILITY-01) — consume exclusivamente
 * `GET /api/v1/accounting/journal-entries`. Solo lectura: sin crear/editar/eliminar asientos
 * desde esta pantalla (el Posting Engine sigue siendo la única vía de escritura).
 */
export function JournalEntriesPage() {
  const navigate = useNavigate();
  const [items, setItems] = useState<JournalEntryListItemDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("");
  const [loading, setLoading] = useState(false);

  const fetchList = useCallback(async () => {
    setLoading(true);
    try {
      const r = await accountingApi.listJournalEntries(page, PAGE_SIZE, {
        status: status || undefined,
      });
      setItems(r.items);
      setTotal(r.totalCount);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, {
          generic: "No se pudo cargar el listado de asientos contables.",
        }),
      );
    } finally {
      setLoading(false);
    }
  }, [page, status]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const columns: ZHDataTableColumn<JournalEntryListItemDto>[] = [
    {
      key: "entryDate",
      header: "Fecha",
      render: (row) => formatDate(row.entryDate),
    },
    {
      key: "entryNumber",
      header: "Número",
      render: (row) => row.entryNumber ?? "—",
    },
    {
      key: "sourceModule",
      header: "Origen",
      render: (row) => row.sourceModule,
    },
    {
      key: "sourceDocumentType",
      header: "Tipo documento",
      render: (row) => row.sourceDocumentType ?? row.sourceEventType,
    },
    {
      key: "sourceDocumentNumber",
      header: "Número documento",
      render: (row) =>
        row.sourceDocumentNumber ?? (
          <code className="prd-sku" title={row.sourceEventId}>
            {row.sourceEventId.slice(0, 8)}…
          </code>
        ),
    },
    {
      key: "sourcePartyName",
      header: "Tercero",
      render: (row) => row.sourcePartyName ?? "—",
    },
    {
      key: "sourceDocumentDate",
      header: "Fecha documento",
      render: (row) => (row.sourceDocumentDate ? formatDate(row.sourceDocumentDate) : "—"),
    },
    {
      key: "description",
      header: "Descripción",
      render: (row) => row.description,
    },
    {
      key: "totalDebit",
      header: "Debe",
      align: "right",
      render: (row) => formatMoney(row.totalDebit),
    },
    {
      key: "totalCredit",
      header: "Haber",
      align: "right",
      render: (row) => formatMoney(row.totalCredit),
    },
    {
      key: "status",
      header: "Estado",
      render: (row) => {
        const b = statusBadge(row.status);
        return <Badge label={b.label} variant={b.variant} />;
      },
    },
    {
      key: "actions",
      header: "",
      align: "right",
      render: (row) => (
        <ZHBtn
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => navigate(`/accounting/journal-entries/${row.id}`)}
        >
          Ver detalle
        </ZHBtn>
      ),
    },
  ];

  return (
    <PageShell
      kicker="Contabilidad"
      title="Asientos contables"
      subtitle="Consulta de asientos generados por el motor de contabilización"
      action={
        <div className="zh-form-actions-row">
          <ZHBtn type="button" variant="ghost" onClick={() => navigate("/accounting")}>
            Contabilidad
          </ZHBtn>
          <ZHBtn type="button" variant="ghost" onClick={() => navigate("/accounting/reports")}>
            Reportes
          </ZHBtn>
          <ZHBtn type="button" variant="ghost" onClick={() => navigate("/accounting/chart-of-accounts")}>
            Plan de cuentas
          </ZHBtn>
        </div>
      }
    >
      <ZHCard
        title="Listado"
        actions={
          <div className="zh-form-actions-row">
            <ZhSelect
              value={status}
              onChange={(e) => {
                setPage(1);
                setStatus(e.target.value);
              }}
            >
              {STATUS_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </ZhSelect>
            <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void fetchList()} disabled={loading}>
              Actualizar
            </ZHBtn>
          </div>
        }
      >
        <ZHDataTable
          columns={columns}
          rows={items}
          rowKey={(row) => row.id}
          loading={loading}
          emptyMessage="No hay asientos contables registrados."
          page={page}
          pageSize={PAGE_SIZE}
          onPageChange={setPage}
          total={total}
        />
      </ZHCard>
    </PageShell>
  );
}
