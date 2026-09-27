import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Badge, NoAccessPage, PageShell, PageToolbar, TableCard } from "../../../components/PageShell";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHTabBar, type ZHTab } from "../../../components/zh/ZHTabBar";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { cajaService, type CashRegisterDto } from "../api/cajaService";
import {
  cashFundingRequestService,
  type CashFundingRequestListItemDto,
  type CashFundingRequestStatus,
} from "../api/cashFundingRequestService";
import {
  CASH_FUNDING_REQUEST_STATUSES,
  cashFundingRequestStatusBadge,
  cashFundingRequestStatusLabel,
} from "../constants/cashFundingRequestStatus";
import { CASH_FUNDING_REQUESTS_ROUTE } from "../facades/cashFundingRequestFacade";

const PAGE_SIZE = 25;

const CASH_FUNDING_PERMISSIONS = {
  view: "caja.funding-requests.view",
  create: "supplier-payments.create",
} as const;

type TabId = "pending" | "history" | "mine";

const TAB_LABEL: Record<TabId, string> = {
  pending: "Pendientes",
  history: "Historial",
  mine: "Mis solicitudes",
};

/**
 * ZH-CASH-FUNDING-REQUEST-UI-FINAL-02E-EF — "Caja > Solicitudes de efectivo". Una sola pantalla,
 * pestañas según permisos: Pendientes/Historial (bandeja de la sucursal activa, requiere
 * `caja.funding-requests.view`) y Mis solicitudes (`supplier-payments.create`). Filtros y
 * paginación siempre en el servidor; el backend es la autoridad de acceso.
 */
export function CashFundingRequestsPage() {
  const navigate = useNavigate();
  const { has } = usePermissionsUi();
  const canView = has(CASH_FUNDING_PERMISSIONS.view);
  const canRequest = has(CASH_FUNDING_PERMISSIONS.create);
  const [searchParams] = useSearchParams();

  const tabs = useMemo<ZHTab<TabId>[]>(() => {
    const ids: TabId[] = [...(canView ? (["pending", "history"] as const) : []), ...(canRequest ? (["mine"] as const) : [])];
    return ids.map((id) => ({ id, label: TAB_LABEL[id] }));
  }, [canView, canRequest]);

  const requestedTab = searchParams.get("tab") as TabId | null;
  const [tab, setTab] = useState<TabId>(() =>
    requestedTab && tabs.some((t) => t.id === requestedTab) ? requestedTab : (tabs[0]?.id ?? "mine"),
  );
  const [items, setItems] = useState<CashFundingRequestListItemDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<CashFundingRequestStatus | "">("");
  const [cashRegisterId, setCashRegisterId] = useState("");
  const [cashRegisters, setCashRegisters] = useState<CashRegisterDto[]>([]);
  const [loading, setLoading] = useState(false);

  // Si los permisos cambian (carga de sesión), la pestaña activa siempre es una permitida.
  useEffect(() => {
    if (tabs.length > 0 && !tabs.some((t) => t.id === tab)) setTab(tabs[0].id);
  }, [tabs, tab]);

  useEffect(() => {
    if (!canView) return;
    // Filtro "Caja": cajas de la sucursal activa (mismo catálogo que Caja). Si no se puede leer,
    // el filtro simplemente no se ofrece.
    cajaService.getCashRegisters().then(setCashRegisters).catch(() => setCashRegisters([]));
  }, [canView]);

  const fetchList = useCallback(async () => {
    setLoading(true);
    try {
      const result =
        tab === "mine"
          ? await cashFundingRequestService.listMine(page, PAGE_SIZE, status || null)
          : await cashFundingRequestService.list(page, PAGE_SIZE, {
              status: tab === "pending" ? "Pending" : status || null,
              cashRegisterId: cashRegisterId || null,
            });
      setItems(result.items);
      setTotal(result.totalCount);
    } catch (err: unknown) {
      setItems([]);
      setTotal(0);
      message.error(
        formatApiRequestError(err, { generic: "No se pudo cargar el listado de solicitudes de efectivo." }),
      );
    } finally {
      setLoading(false);
    }
  }, [tab, page, status, cashRegisterId]);

  useEffect(() => {
    if (tabs.length === 0) return;
    void fetchList();
  }, [fetchList, tabs.length]);

  const changeTab = (next: TabId) => {
    setTab(next);
    setPage(1);
    setStatus("");
    setCashRegisterId("");
  };

  const resetFilters = () => {
    setStatus("");
    setCashRegisterId("");
    setPage(1);
  };

  const columns = useMemo<ZHDataTableColumn<CashFundingRequestListItemDto>[]>(
    () => [
      { key: "supplier", header: "Proveedor", render: (row) => <strong>{row.supplierName || "—"}</strong> },
      { key: "cashRegister", header: "Caja", render: (row) => row.cashRegisterName || "—" },
      { key: "requestedBy", header: "Solicitado por", render: (row) => row.requestedByName || "—" },
      {
        key: "cashAmount",
        header: "Efectivo solicitado",
        align: "right",
        cellClassName: "zh-table-cell--num",
        render: (row) => <ZHMoneyValue value={row.cashAmount} precision="money" emphasis="strong" />,
      },
      {
        key: "totalAmount",
        header: "Pago total",
        align: "right",
        cellClassName: "zh-table-cell--num",
        render: (row) => <ZHMoneyValue value={row.totalAmount} precision="money" />,
      },
      { key: "requestedAt", header: "Fecha", render: (row) => formatDateTime(row.requestedAtUtc) },
      {
        key: "status",
        header: "Estado",
        render: (row) => (
          <Badge label={cashFundingRequestStatusLabel(row.status)} variant={cashFundingRequestStatusBadge(row.status)} />
        ),
      },
      {
        key: "actions",
        header: "Acción",
        align: "right",
        render: (row) => (
          <ZHBtn
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => navigate(`${CASH_FUNDING_REQUESTS_ROUTE}/${row.id}`)}
          >
            Ver
          </ZHBtn>
        ),
      },
    ],
    [navigate],
  );

  if (tabs.length === 0) return <NoAccessPage title="Solicitudes de efectivo" />;

  const showStatusFilter = tab !== "pending";
  const showCashRegisterFilter = tab !== "mine" && cashRegisters.length > 0;

  return (
    <PageShell
      kicker="Caja"
      title="Solicitudes de efectivo"
      subtitle="Efectivo solicitado a una caja operada por otro usuario para completar un pago a proveedor."
    >
      <ZHTabBar tabs={tabs} activeTab={tab} onChange={changeTab} ariaLabel="Solicitudes de efectivo" />
      <TableCard>
        {(showStatusFilter || showCashRegisterFilter) && (
          <PageToolbar>
            <ZHFilterBar onClear={resetFilters} clearLabel="Restablecer filtros">
              {showStatusFilter && (
                <ZHField label="Estado">
                  <ZhSelect
                    aria-label="Estado"
                    value={status}
                    onChange={(event) => {
                      setStatus(event.target.value as CashFundingRequestStatus | "");
                      setPage(1);
                    }}
                  >
                    <option value="">Todos</option>
                    {CASH_FUNDING_REQUEST_STATUSES.map((s) => (
                      <option key={s} value={s}>
                        {cashFundingRequestStatusLabel(s)}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
              )}
              {showCashRegisterFilter && (
                <ZHField label="Caja">
                  <ZhSelect
                    aria-label="Caja"
                    value={cashRegisterId}
                    onChange={(event) => {
                      setCashRegisterId(event.target.value);
                      setPage(1);
                    }}
                  >
                    <option value="">Todas</option>
                    {cashRegisters.map((r) => (
                      <option key={r.id} value={r.id}>
                        {r.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
              )}
            </ZHFilterBar>
          </PageToolbar>
        )}
        <ZHDataTable
          columns={columns}
          rows={items}
          rowKey={(row) => row.id}
          loading={loading}
          showRowNumber
          rowNumberOffset={(page - 1) * PAGE_SIZE}
          emptyMessage={
            tab === "pending"
              ? "No hay solicitudes de efectivo pendientes."
              : "No hay solicitudes de efectivo para estos filtros."
          }
          page={page}
          pageSize={PAGE_SIZE}
          onPageChange={setPage}
          total={total}
        />
      </TableCard>
    </PageShell>
  );
}

export default CashFundingRequestsPage;
