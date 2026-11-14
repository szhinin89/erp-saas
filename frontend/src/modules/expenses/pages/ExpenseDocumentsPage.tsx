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
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useI18n } from "../../../i18n/i18n";
import { formatDate } from "../../../lib/formatters/dateFormatters";
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
  const { t } = useI18n();
  const { has } = usePermissionsUi();
  const canView = has(PERMISSIONS.view);
  const canCreate = has(PERMISSIONS.create);
  const navigate = useNavigate();

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
        generic: t("expenses.documents.loadError"),
      });
      setError(msg);
      message.error(msg);
    } finally {
      setLoading(false);
    }
  }, [page, search, status, t]);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  const columns = useMemo<ZHDataTableColumn<ExpenseDocumentListItemDto>[]>(
    () => [
      {
        key: "document",
        header: t("expenses.documents.col.document"),
        render: (row) => (
          <div className="exp-doc-list-cell">
            <strong>{row.documentNumber}</strong>
            <span>{row.documentType}</span>
          </div>
        ),
      },
      {
        key: "supplier",
        header: t("expenses.documents.col.supplier"),
        render: (row) => (
          <div className="exp-doc-list-cell">
            <strong>{row.supplierName}</strong>
            <span>{row.supplierTaxId}</span>
          </div>
        ),
      },
      { key: "issueDate", header: t("expenses.documents.col.issueDate"), render: (row) => formatDate(row.issueDate) },
      { key: "dueDate", header: t("expenses.documents.col.dueDate"), render: (row) => formatDate(row.dueDate) },
      {
        key: "status",
        header: t("common.status"),
        render: (row) => <ExpenseDocumentStatusBadge status={row.status} />,
      },
      {
        key: "lines",
        header: t("expenses.documents.col.lines"),
        align: "right",
        render: (row) => row.lineCount,
      },
      {
        key: "total",
        header: t("common.total"),
        align: "right",
        render: (row) => (
          <ZHMoneyValue value={row.grandTotal} precision="money" />
        ),
      },
    ],
    [t],
  );

  const hasFilters = search.trim() !== "" || status !== "";

  if (!canView) return <NoAccessPage title={t("app.nav.group.expenses")} />;

  return (
    <PageShell
      kicker={t("app.nav.group.expenses")}
      title={t("app.nav.item.expenses.documents")}
      subtitle={t("expenses.documents.subtitle")}
      action={
        canCreate ? (
          <ZHLinkButton to="/expenses/documents/new" variant="primary">
            <span className="material-symbols-outlined" aria-hidden="true">
              add
            </span>
            {t("expenses.documents.newExpense")}
          </ZHLinkButton>
        ) : null
      }
    >
      <TableCard>
        <PageToolbar>
          <ZHFilterBar plain>
            <ZHField label={t("expenses.documents.filter.search")} className="zh-filterbar__field">
              <ZhTextInput
                value={search}
                placeholder={t("expenses.documents.filter.searchPlaceholder")}
                onChange={(event) => {
                  setSearch(event.target.value);
                  setPage(1);
                }}
              />
            </ZHField>
            <ZHField label={t("common.status")} className="zh-filterbar__field">
              <ZhSelect
                value={status}
                onChange={(event) => {
                  setStatus(event.target.value);
                  setPage(1);
                }}
              >
                <option value="">{t("expenses.documents.filter.all")}</option>
                <option value="Draft">{t("expenses.documents.status.draft")}</option>
                <option value="Confirmed">{t("expenses.documents.status.confirmed")}</option>
                <option value="Cancelled">{t("expenses.documents.status.cancelled")}</option>
              </ZhSelect>
            </ZHField>
            <ZHBtn type="button" variant="secondary" onClick={() => void load()}>
              <span className="material-symbols-outlined" aria-hidden="true">
                refresh
              </span>
              {t("common.refresh")}
            </ZHBtn>
          </ZHFilterBar>
        </PageToolbar>

        {error ? (
          <EmptyState message={error} />
        ) : (
          <ZHDataTable
            rows={rows}
            columns={columns}
            rowKey={(row) => row.id}
            loading={loading}
            showRowNumber
            rowNumberOffset={(page - 1) * 25}
            emptyMessage={t(
              hasFilters
                ? "expenses.documents.empty.filteredTitle"
                : "expenses.documents.empty.title",
            )}
            emptyIcon="receipt_long"
            emptyDescription={t(
              hasFilters
                ? "expenses.documents.empty.filteredDescription"
                : "expenses.documents.empty.description",
            )}
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
