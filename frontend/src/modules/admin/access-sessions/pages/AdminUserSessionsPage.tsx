import { useCallback, useEffect, useState } from "react";
import {
  Badge,
  ErrorState,
  NoAccessPage,
  type BadgeVariant,
} from "../../../../components/PageShell";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHConfirmModal } from "../../../../components/zh/ZHConfirmModal";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import {
  ZHDataTable,
  type ZHDataTableColumn,
} from "../../../../components/zh/ZHDataTable";
import { ZhDateInput, ZhSelect } from "../../../../components/zh/inputs";
import {
  companyDayUtcRange,
  formatDateTime,
} from "../../../../lib/formatters/dateFormatters";
import {
  userSessionAdminService,
  type SessionStatisticsDto,
  type UserSessionAdminDto,
} from "../../api/userSessionAdminService";
import {
  membershipLookupFacade,
  type CompanyUserMembershipAdminDto,
} from "../../../access/facades/membershipLookupFacade";
import { formatApiError } from "../../../lib/formatApiError";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { useI18n } from "../../../../i18n/i18n";

type SessionStatus = UserSessionAdminDto["status"];

const STATUS_BADGE: Record<SessionStatus, BadgeVariant> = {
  Active: "success",
  ClosedManually: "neutral",
  ClosedByNewLogin: "neutral",
  Expired: "warning",
};

const STATUSES: SessionStatus[] = [
  "Active",
  "ClosedManually",
  "ClosedByNewLogin",
  "Expired",
];

const STATS_KEY: Record<SessionStatus, keyof SessionStatisticsDto> = {
  Active: "active",
  ClosedManually: "closedManually",
  ClosedByNewLogin: "closedByNewLogin",
  Expired: "expired",
};

const PAGE_SIZE = 25;

function shortId(id: string): string {
  return `${id.slice(0, 8)}…`;
}

export function AdminUserSessionsPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("access.sessions.view");
  const canClose = canShow("access.sessions.close");
  // El selector de usuario reutiliza la facade de membresías de la empresa activa; si el perfil no
  // tiene ese permiso, el filtro por usuario simplemente no se ofrece (la tabla ya muestra nombres
  // resueltos por el backend, sin depender de este permiso).
  const canPickUser = canShow("access.company_user_memberships.view");

  const [identityUserId, setIdentityUserId] = useState("");
  const [status, setStatus] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [pageNumber, setPageNumber] = useState(1);

  const [rows, setRows] = useState<UserSessionAdminDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [stats, setStats] = useState<SessionStatisticsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [closingId, setClosingId] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [members, setMembers] = useState<CompanyUserMembershipAdminDto[]>([]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [paged, statistics] = await Promise.all([
        userSessionAdminService.getPaged({
          identityUserId: identityUserId || undefined,
          status: status || undefined,
          // Días de empresa (Company.Timezone) → rango instante UTC [desde, hasta).
          fromUtc: fromDate ? companyDayUtcRange(fromDate)?.startUtc : undefined,
          toUtc: toDate ? companyDayUtcRange(toDate)?.endUtcExclusive : undefined,
          pageNumber,
          pageSize: PAGE_SIZE,
        }),
        userSessionAdminService.getStatistics(),
      ]);
      setRows(paged.items);
      setTotalCount(paged.totalCount);
      setStats(statistics);
    } catch (e) {
      setError(formatApiError(e));
      setRows([]);
      setStats(null);
    } finally {
      setLoading(false);
    }
  }, [identityUserId, status, fromDate, toDate, pageNumber]);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  useEffect(() => {
    if (!canView || !canPickUser) return;
    membershipLookupFacade
      .list(false)
      .then(setMembers)
      .catch(() => setMembers([]));
  }, [canView, canPickUser]);

  if (!canView)
    return <NoAccessPage title={t("app.nav.item.admin.accessSessions")} />;

  /** Cualquier cambio de filtro vuelve a la primera página. */
  function changeFilter(setter: (value: string) => void) {
    return (value: string) => {
      setter(value);
      setPageNumber(1);
    };
  }

  function clearFilters() {
    setIdentityUserId("");
    setStatus("");
    setFromDate("");
    setToDate("");
    setPageNumber(1);
  }

  const hasFilters = Boolean(identityUserId || status || fromDate || toDate);

  const sessionColumns: ZHDataTableColumn<UserSessionAdminDto>[] = [
    {
      key: "user",
      header: t("admin.accessSessions.column.user"),
      render: (row) =>
        row.userFullName || row.username ? (
          <div>
            <div>{row.userFullName || row.username}</div>
            {row.userFullName && row.username && (
              <div className="zh-text-muted zh-text-xs">@{row.username}</div>
            )}
          </div>
        ) : (
          <span title={row.identityUserId}>{shortId(row.identityUserId)}</span>
        ),
    },
    {
      key: "branch",
      header: t("admin.accessSessions.column.branch"),
      render: (row) =>
        row.branchName || (
          <span title={row.branchId}>{shortId(row.branchId)}</span>
        ),
    },
    {
      key: "terminal",
      header: t("admin.accessSessions.column.terminal"),
      render: (row) => row.terminalId,
    },
    {
      key: "status",
      header: t("admin.accessSessions.column.status"),
      render: (row) => (
        <Badge
          variant={STATUS_BADGE[row.status]}
          label={t(`admin.accessSessions.status.${row.status}`)}
        />
      ),
    },
    {
      key: "createdAt",
      header: t("admin.accessSessions.column.startedAt"),
      render: (row) => formatDateTime(row.createdAt),
    },
    {
      key: "updatedAt",
      header: t("admin.accessSessions.column.updatedAt"),
      render: (row) => (row.updatedAt ? formatDateTime(row.updatedAt) : "—"),
    },
    {
      key: "action",
      header: t("admin.accessSessions.column.action"),
      cellClassName: "zh-table-cell--actions",
      render: (row) =>
        canClose && row.status === "Active" ? (
          <ZHBtn
            variant="destructive"
            size="sm"
            type="button"
            onClick={() => setClosingId(row.sessionId)}
          >
            {t("admin.accessSessions.close.action")}
          </ZHBtn>
        ) : null,
    },
  ];

  async function confirmClose() {
    if (!closingId) return;
    const id = closingId;
    setClosingId(null);
    try {
      await userSessionAdminService.close(id);
      setNotice(t("admin.accessSessions.close.success"));
      await load();
    } catch (e) {
      setError(formatApiError(e));
    }
  }

  return (
    <ErpPageTemplate
      kicker={t("app.nav.group.admin")}
      title={t("app.nav.item.admin.accessSessions")}
      subtitle={t("admin.accessSessions.subtitle")}
      action={
        <ZHBtn
          variant="secondary"
          type="button"
          disabled={loading}
          onClick={() => void load()}
        >
          <span className="material-symbols-outlined">refresh</span>
          {t("common.refresh")}
        </ZHBtn>
      }
    >
      {error && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={error}
        />
      )}
      {notice && <ZHPageNotice variant="success" message={notice} />}

      <div className="pg-section">
        {/* Los indicadores son también filtros rápidos por estado: una sola fila compacta. */}
        <ZHFilterBar
          plain
          disabled={loading}
          onClear={hasFilters ? clearFilters : undefined}
          chips={
            <>
              <ZHBtn
                variant={status === "" ? "primary" : "secondary"}
                size="sm"
                type="button"
                aria-pressed={status === ""}
                onClick={() => changeFilter(setStatus)("")}
              >
                {t("admin.accessSessions.stats.all")} {stats?.total ?? "—"}
              </ZHBtn>
              {STATUSES.map((s) => (
                <ZHBtn
                  key={s}
                  variant={status === s ? "primary" : "secondary"}
                  size="sm"
                  type="button"
                  aria-pressed={status === s}
                  onClick={() => changeFilter(setStatus)(s)}
                >
                  {t(`admin.accessSessions.stats.${STATS_KEY[s]}`)}{" "}
                  {stats?.[STATS_KEY[s]] ?? "—"}
                </ZHBtn>
              ))}
            </>
          }
        >
          {canPickUser && (
            <div className="zh-filterbar__field">
              <ZHField label={t("admin.accessSessions.filter.user")} density="compact">
                <ZhSelect
                  className="zh-input"
                  value={identityUserId}
                  onChange={(e) => changeFilter(setIdentityUserId)(e.target.value)}
                >
                  <option value="">{t("admin.accessSessions.filter.allUsers")}</option>
                  {members.map((m) => (
                    <option key={m.identityUserId} value={m.identityUserId}>
                      {m.fullName} (@{m.username})
                    </option>
                  ))}
                </ZhSelect>
              </ZHField>
            </div>
          )}
          <div className="zh-filterbar__field">
            <ZHField label={t("admin.accessSessions.filter.status")} density="compact">
              <ZhSelect
                className="zh-input"
                value={status}
                onChange={(e) => changeFilter(setStatus)(e.target.value)}
              >
                <option value="">{t("admin.accessSessions.filter.allStatuses")}</option>
                {STATUSES.map((s) => (
                  <option key={s} value={s}>
                    {t(`admin.accessSessions.status.${s}`)}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHField label={t("admin.accessSessions.filter.from")} density="compact">
              <ZhDateInput
                className="zh-input"
                aria-label={t("admin.accessSessions.filter.from")}
                value={fromDate}
                onChange={(e) => changeFilter(setFromDate)(e.target.value)}
              />
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHField label={t("admin.accessSessions.filter.to")} density="compact">
              <ZhDateInput
                className="zh-input"
                aria-label={t("admin.accessSessions.filter.to")}
                value={toDate}
                onChange={(e) => changeFilter(setToDate)(e.target.value)}
              />
            </ZHField>
          </div>
        </ZHFilterBar>

        {error && !loading ? (
          <div className="pg-pad-40">
            <ErrorState message={error} />
            <ZHBtn variant="secondary" type="button" onClick={() => void load()}>
              {t("common.refresh")}
            </ZHBtn>
          </div>
        ) : (
          <ZHDataTable
            columns={sessionColumns}
            rows={rows}
            rowKey={(row) => row.sessionId}
            loading={loading}
            emptyMessage={t("admin.accessSessions.empty")}
            page={pageNumber}
            pageSize={PAGE_SIZE}
            total={totalCount}
            onPageChange={setPageNumber}
            showRowNumber
            rowNumberOffset={(pageNumber - 1) * PAGE_SIZE}
            tableClassName="table--compact"
          />
        )}
      </div>

      <ZHConfirmModal
        open={closingId !== null}
        title={t("admin.accessSessions.close.action")}
        message={t("admin.accessSessions.close.message")}
        variant="danger"
        confirmLabel={t("admin.accessSessions.close.action")}
        onConfirm={() => void confirmClose()}
        onCancel={() => setClosingId(null)}
      />
    </ErpPageTemplate>
  );
}
