import { useCallback, useEffect, useState } from "react";
import { Badge, NoAccessPage } from "../../../../components/PageShell";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import { ZhSelect } from "../../../../components/zh/inputs";
import {
  ZHDataTable,
  type ZHDataTableColumn,
} from "../../../../components/zh/ZHDataTable";
import { useI18n } from "../../../../i18n/i18n";
import { formatDateTime } from "../../../../lib/formatters/dateFormatters";
import {
  activityService,
  type UserActivityDto,
} from "../../api/activityService";
import { formatApiError } from "../../../lib/formatApiError";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";

/**
 * Módulos que hoy escriben en la bitácora (códigos exactos que persiste el backend). El filtro del
 * servidor es por igualdad exacta, por eso se ofrece como lista cerrada y no como texto libre.
 */
const ACTIVITY_MODULES = ["branches", "inventory", "masterdata", "companies"];

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

const pageSize = 25;

export function ActivityPage() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canView = canShow("admin.activity.view");

  const [rows, setRows] = useState<UserActivityDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [module, setModule] = useState("");
  const [page, setPage] = useState(1);

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

  /** Etiqueta legible de la acción completa; si no está catalogada, cae al verbo genérico. */
  const actionLabel = (action: string) =>
    t(`admin.activity.action.${action}`, t(actionVerbI18nKey(action)));
  /** Etiqueta legible del módulo; si no está catalogado, muestra el código tal cual. */
  const moduleLabel = (code: string) =>
    t(`admin.activity.module.${code}`, code);

  const activityColumns: ZHDataTableColumn<UserActivityDto>[] = [
    {
      key: "when",
      header: t("audit.column.when"),
      render: (row) => formatDateTime(row.createdAt),
    },
    {
      key: "module",
      header: t("admin.activity.column.module"),
      render: (row) => <Badge variant="neutral" label={moduleLabel(row.module)} />,
    },
    {
      key: "what",
      header: t("audit.column.what"),
      render: (row) => actionLabel(row.action),
    },
    {
      key: "detail",
      header: t("audit.column.detail"),
      render: (row) =>
        row.description || row.entityType ? (
          <div>
            <div>{row.description || row.entityType}</div>
            {row.description && row.entityType && (
              <div className="zh-text-muted zh-text-xs">{row.entityType}</div>
            )}
          </div>
        ) : (
          "—"
        ),
    },
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
          {t("common.refresh")}
        </ZHBtn>
      }
    >
      <ZHPageNotice variant="info" message={t("admin.activity.scopeNotice")} />
      {error && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={error}
        />
      )}

      <div className="pg-section">
        <ZHFilterBar
          plain
          disabled={loading}
          onClear={
            module
              ? () => {
                  setModule("");
                  setPage(1);
                }
              : undefined
          }
        >
          <div className="zh-filterbar__field">
            <ZHField label={t("admin.activity.filter.module")} density="compact">
              <ZhSelect
                className="zh-input"
                value={module}
                onChange={(e) => {
                  setModule(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">{t("admin.activity.filter.allModules")}</option>
                {ACTIVITY_MODULES.map((code) => (
                  <option key={code} value={code}>
                    {moduleLabel(code)}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </div>
        </ZHFilterBar>

        <ZHDataTable
          columns={activityColumns}
          rows={rows}
          rowKey={(row) => row.id}
          loading={loading}
          emptyMessage={t("admin.activity.empty")}
          page={page}
          pageSize={pageSize}
          onPageChange={setPage}
          showRowNumber
          rowNumberOffset={(page - 1) * pageSize}
          tableClassName="table--compact"
        />
      </div>
    </ErpPageTemplate>
  );
}
