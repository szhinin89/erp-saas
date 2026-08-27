import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  EmptyState,
  NoAccessPage,
  PageShell,
  PageToolbar,
  TableCard,
} from "../../../components/PageShell";
import { ZHBtn, ZHField, ZHLinkButton } from "../../../components/zh/ZHForm";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import {
  expenseDocumentService,
  type ExpenseDocumentListItemDto,
} from "../api/expenseDocumentService";
import { ExpenseDocumentStatusBadge } from "../components/ExpenseDocumentStatusBadge";
import "../styles/expense-documents.css";

const PERMISSIONS = {
  view: "expenses.documents.view",
  create: "expenses.documents.create",
} as const;

export function ExpenseDocumentsPage() {
  const { has } = usePermissionsUi();
  const canView = has(PERMISSIONS.view);
  const canCreate = has(PERMISSIONS.create);
  const navigate = useNavigate();
  const decimals = getDecimalConfig().totalAmount;

  const [rows, setRows] = useState<ExpenseDocumentListItemDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await expenseDocumentService.list(search, status, page, 25);
      setRows(response.items);
      setTotal(response.total);
    } catch (err) {
      const msg = formatApiRequestError(err, {
        generic: "No se pudo cargar documentos de gasto.",
      });
      setError(msg);
      message.error(msg);
    } finally {
      setLoading(false);
    }
  }, [page, search, status]);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  const columns = useMemo<ZHDataTableColumn<ExpenseDocumentListItemDto>[]>(
    () => [
      {
        key: "document",
        header: "Documento",
        render: (row) => (
          <div className="exp-doc-list-cell">
            <strong>{row.documentNumber}</strong>
            <span>{row.documentType}</span>
          </div>
        ),
      },
      {
        key: "supplier",
        header: "Proveedor",
        render: (row) => (
          <div className="exp-doc-list-cell">
            <strong>{row.supplierName}</strong>
            <span>{row.supplierTaxId}</span>
          </div>
        ),
      },
      { key: "issueDate", header: "Emision", render: (row) => formatDate(row.issueDate) },
      { key: "dueDate", header: "Vence", render: (row) => formatDate(row.dueDate) },
      {
        key: "status",
        header: "Estado",
        render: (row) => <ExpenseDocumentStatusBadge status={row.status} />,
      },
      {
        key: "lines",
        header: "Lineas",
        align: "right",
        render: (row) => row.lineCount,
      },
      {
        key: "total",
        header: "Total",
        align: "right",
        render: (row) => (
          <ZHMoneyValue value={row.grandTotal} decimals={decimals} />
        ),
      },
    ],
    [decimals],
  );

  if (!canView) return <NoAccessPage title="Gastos" />;

  return (
    <PageShell
      kicker="Gastos"
      title="Documentos de gasto"
      subtitle="Registro de gastos por proveedor, sin productos ni movimientos de inventario."
      action={
        canCreate ? (
          <ZHLinkButton to="/expenses/documents/new" variant="primary">
            <span className="material-symbols-outlined" aria-hidden="true">
              add
            </span>
            Nuevo gasto
          </ZHLinkButton>
        ) : null
      }
    >
      <TableCard>
        <PageToolbar>
          <ZHField label="Buscar">
            <ZhTextInput
              value={search}
              placeholder="Proveedor o numero..."
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
            />
          </ZHField>
          <ZHField label="Estado">
            <ZhSelect
              value={status}
              onChange={(event) => {
                setStatus(event.target.value);
                setPage(1);
              }}
            >
              <option value="">Todos</option>
              <option value="Draft">Borrador</option>
              <option value="Confirmed">Confirmado</option>
              <option value="Cancelled">Anulado</option>
            </ZhSelect>
          </ZHField>
          <ZHBtn type="button" variant="secondary" onClick={() => void load()}>
            <span className="material-symbols-outlined" aria-hidden="true">
              refresh
            </span>
            Actualizar
          </ZHBtn>
        </PageToolbar>

        {error ? (
          <EmptyState message={error} />
        ) : (
          <ZHDataTable
            rows={rows}
            columns={columns}
            rowKey={(row) => row.id}
            loading={loading}
            emptyMessage="No hay documentos de gasto registrados."
            page={page}
            pageSize={25}
            total={total}
            onPageChange={setPage}
            onRowClick={(row) => navigate(`/expenses/documents/${row.id}`)}
          />
        )}
      </TableCard>
    </PageShell>
  );
}

export default ExpenseDocumentsPage;
