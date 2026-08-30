import { useCallback, useEffect, useState } from "react";
import {
  EmptyState,
  LoadingState,
  NoAccessPage,
} from "../../../../components/PageShell";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZhTextInput } from "../../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { useI18n } from "../../../../i18n/i18n";
import { formatDateTimeSeconds } from "../../../../lib/formatters/dateFormatters";
import {
  activityService,
  type UserActivityDto,
} from "../../api/activityService";
import { formatApiError } from "../../../lib/formatApiError";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";

function actionVerbI18nKey(action: string): string {
  const i = action.lastIndexOf(".");
  const verb = (i >= 0 ? action.slice(i + 1) : action).toLowerCase();
  switch (verb) {
    case "create":
      return "audit.action.create";
    case "update":
      return "audit.action.update";
    case "enable":
      return "audit.action.enable";
    case "disable":
      return "audit.action.disable";
    default:
      return "audit.action.unknown";
  }
}

export function ActivityPage() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canView = canShow("admin.activity.view");

  const [rows, setRows] = useState<UserActivityDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [module, setModule] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 25;

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await activityService.my({
        module: module || undefined,
        page,
        pageSize,
      });
      setRows(data ?? []);
    } catch (e) {
      setError(formatApiError(e));
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [module, page]);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  if (!canView)
    return <NoAccessPage title={t("app.nav.item.admin.activity")} />;

  const activityColumns: ZHDataTableColumn<UserActivityDto>[] = [
    { key: "when", header: t("audit.column.when"), render: (row) => formatDateTimeSeconds(row.createdAt) },
    { key: "who", header: t("audit.column.who"), render: (row) => row.userFullName || row.userEmail || "—" },
    { key: "module", header: "Módulo", render: (row) => row.module },
    { key: "what", header: t("audit.column.what"), render: (row) => t(actionVerbI18nKey(row.action)) },
    { key: "detail", header: t("audit.column.detail"), render: (row) => row.description || row.entityType || "—" },
  ];

  return (
    <ErpPageTemplate
      kicker={t("app.nav.group.admin")}
      title={t("app.nav.item.admin.activity")}
      subtitle={t("admin.activity.subtitle")}
      action={
        <ZHBtn
          variant="secondary"
          type="button"
          disabled={loading}
          onClick={() => void load()}
        >
          <span className="material-symbols-outlined">refresh</span>
          Actualizar
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

      <div className="pg-section">
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <ZhTextInput
              className="zh-input"
              placeholder="Filtrar por módulo (opcional)…"
              value={module}
              onChange={(e) => {
                setModule(e.target.value);
                setPage(1);
              }}
            />
          </div>
          <div className="pg-table-controls-right">
            <span>Pág. {page}</span>
          </div>
        </div>

        {loading ? (
          <div className="pg-pad-40">
            <LoadingState />
          </div>
        ) : rows.length === 0 ? (
          <div className="pg-pad-40">
            <EmptyState message={t("audit.empty")} />
          </div>
        ) : (
          <ZHDataTable
            columns={activityColumns}
            rows={rows}
            rowKey={(row) => row.id}
            showRowNumber
            rowNumberOffset={(page - 1) * pageSize}
          />
        )}

        <div className="pg-table-footer">
          <div className="pg-pagination-controls">
            <button
              className="pg-pagination-btn"
              type="button"
              disabled={page <= 1 || loading}
              onClick={() => setPage((p) => p - 1)}
            >
              <span className="material-symbols-outlined">chevron_left</span>
            </button>
            <button
              className="pg-pagination-btn"
              type="button"
              disabled={rows.length < pageSize || loading}
              onClick={() => setPage((p) => p + 1)}
            >
              <span className="material-symbols-outlined">chevron_right</span>
            </button>
          </div>
        </div>
      </div>
    </ErpPageTemplate>
  );
}
