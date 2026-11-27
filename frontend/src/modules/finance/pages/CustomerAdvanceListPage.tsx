import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Badge, PageShell, PageToolbar, TableCard } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useI18n } from "../../../i18n/i18n";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatApiRequestError } from "../../lib/apiError";
import {
  customerAdvanceService,
  type CustomerAdvanceListFilters,
  type CustomerAdvanceListItemDto,
} from "../api/customerAdvanceService";
import { CustomerAdvanceFilters } from "../components/CustomerAdvanceFilters";
import { RegisterCustomerAdvanceModal } from "../components/RegisterCustomerAdvanceModal";
import { useCustomerAdvanceCatalogs } from "../hooks/useCustomerAdvanceCatalogs";
import {
  advanceStatusKey,
  advanceStatusVariant,
  CUSTOMER_ADVANCE_PERMISSIONS,
  CUSTOMER_ADVANCES_ROUTE,
} from "../utils/customerAdvance";

const PAGE_SIZE = 25;

/** Anticipos de clientes: consulta paginada en el servidor, registro y acceso al detalle. */
export function CustomerAdvanceListPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { canShow } = usePermissionsUi();
  const catalogs = useCustomerAdvanceCatalogs();
  const [items, setItems] = useState<CustomerAdvanceListItemDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<CustomerAdvanceListFilters>({});
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [registering, setRegistering] = useState(false);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const result = await customerAdvanceService.list(page, PAGE_SIZE, filters);
      setItems(result.items);
      setTotal(result.total);
    } catch (err: unknown) {
      setItems([]);
      setTotal(0);
      setError(formatApiRequestError(err, { generic: t("finance.customerAdvances.list.error") }));
    } finally {
      setLoading(false);
    }
  }, [page, filters, t]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  // Cliente (con el acceso al detalle), saldo y estado primero: visibles a 390 px sin desplazar.
  const columns: ZHDataTableColumn<CustomerAdvanceListItemDto>[] = [
    {
      key: "customer",
      header: t("finance.customerAdvances.field.customer"),
      // Medio y referencia distinguen los anticipos de un mismo cliente.
      render: (a) => (
        <div className="zh-stack">
          <strong>{a.customerName ?? "—"}</strong>
          <span className="zh-text-muted">{catalogs.methodName(a.paymentMethodId)}</span>
          {a.externalReference ? (
            <span className="zh-text-muted">
              {t("finance.customerAdvances.field.reference")}: {a.externalReference}
            </span>
          ) : null}
          <div>
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => navigate(`${CUSTOMER_ADVANCES_ROUTE}/${a.id}`)}
            >
              {t("common.view")}
            </ZHBtn>
          </div>
        </div>
      ),
    },
    {
      key: "available",
      header: t("finance.customerAdvances.field.available"),
      align: "right",
      render: (a) => <ZHMoneyValue precision="money" value={a.availableAmount} emphasis="strong" />,
    },
    {
      key: "status",
      header: t("finance.customerAdvances.field.status"),
      render: (a) => (
        <Badge variant={advanceStatusVariant(a.status)} label={t(advanceStatusKey(a.status))} />
      ),
    },
    {
      key: "date",
      header: t("finance.customerAdvances.field.advanceDate"),
      render: (a) => formatDate(a.advanceDate),
    },
    {
      key: "branch",
      header: t("finance.customerAdvances.field.branch"),
      render: (a) => catalogs.branchName(a.branchId),
    },
    {
      key: "original",
      header: t("finance.customerAdvances.field.originalAmount"),
      align: "right",
      render: (a) => <ZHMoneyValue precision="money" value={a.originalAmount} />,
    },
  ];

  return (
    <PageShell
      kicker={t("finance.customerAdvances.kicker")}
      title={t("finance.customerAdvances.title")}
      subtitle={t("finance.customerAdvances.list.subtitle")}
      action={
        canShow(CUSTOMER_ADVANCE_PERMISSIONS.create) ? (
          <ZHBtn type="button" onClick={() => setRegistering(true)}>
            {t("finance.customerAdvances.register.title")}
          </ZHBtn>
        ) : undefined
      }
    >
      {error ? (
        <>
          <ZHPageNotice variant="error" message={error} />
          <ZHBtn type="button" variant="ghost" onClick={() => void fetchList()}>
            {t("common.retry")}
          </ZHBtn>
        </>
      ) : null}
      <TableCard>
        <PageToolbar>
          <CustomerAdvanceFilters
            value={filters}
            branches={catalogs.branches}
            onChange={(patch) => {
              setFilters((current) => ({ ...current, ...patch }));
              setPage(1);
            }}
            onReset={() => {
              setFilters({});
              setPage(1);
            }}
          />
        </PageToolbar>
        <ZHDataTable
          columns={columns}
          rows={items}
          rowKey={(a) => a.id}
          loading={loading}
          showRowNumber
          rowNumberOffset={(page - 1) * PAGE_SIZE}
          emptyMessage={t("finance.customerAdvances.list.empty")}
          tableClassName="table--align-top"
          page={page}
          pageSize={PAGE_SIZE}
          onPageChange={setPage}
          total={total}
        />
      </TableCard>
      <RegisterCustomerAdvanceModal
        open={registering}
        onClose={() => setRegistering(false)}
        onRegistered={() => void fetchList()}
      />
    </PageShell>
  );
}
