import { useMemo } from "react";
import { useI18n } from "../../../i18n/i18n";
import { ZHBtn } from "../../../components/zh/ZHForm";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";
import type {
  PartnerActivityItem,
  PartnerUiState,
} from "../store/masterDataPartnerUiStore";
import type { StoreApi, UseBoundStore } from "zustand";

type UiStore = UseBoundStore<StoreApi<PartnerUiState>>;
type Role = "customer" | "supplier";

interface Props {
  role: Role;
  partners: BusinessPartnerSummaryDto[];
  totalCount: number;
  store: UiStore;
}

function timeAgo(date: Date): string {
  const diff = Date.now() - date.getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "ahora mismo";
  if (mins < 60) return `hace ${mins} min`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `hace ${hrs} h`;
  return `hace ${Math.floor(hrs / 24)} d`;
}

const ACTIVITY_ICONS: Record<string, { icon: string; cls: string }> = {
  created: { icon: "person_add", cls: "prd-activity__dot--green" },
  updated: { icon: "edit", cls: "prd-activity__dot--blue" },
  assigned: { icon: "link", cls: "prd-activity__dot--green" },
  revoked: { icon: "link_off", cls: "prd-activity__dot--red" },
  disabled: { icon: "visibility_off", cls: "prd-activity__dot--red" },
  enabled: { icon: "visibility", cls: "prd-activity__dot--green" },
};

function ActivityRow({
  item,
  t,
}: {
  item: PartnerActivityItem;
  t: (key: string, fb?: string) => string;
}) {
  const meta = ACTIVITY_ICONS[item.action] ?? ACTIVITY_ICONS.updated;
  return (
    <div className="prd-activity__row">
      <div className={`prd-activity__dot ${meta.cls}`}>
        <span className="material-symbols-outlined zh-icon-sm">
          {meta.icon}
        </span>
      </div>
      <div className="prd-activity__info">
        <span className="prd-activity__name">{item.partnerName}</span>
        <span className="prd-activity__action">
          {t(`masterdata.activity.action.${item.action}`, item.action)}
        </span>
      </div>
      <span className="prd-activity__time">{timeAgo(item.timestamp)}</span>
    </div>
  );
}

export function MasterDataPartnerResumenTab({
  role,
  partners,
  totalCount,
  store,
}: Props) {
  const { t } = useI18n();
  const activity = store((s) => s.recentActivity);
  const setActiveTab = store((s) => s.setActiveTab);

  const prefix =
    role === "customer" ? "masterdata.customers" : "masterdata.suppliers";
  const icon = role === "customer" ? "groups" : "local_shipping";

  const stats = useMemo(
    () => ({
      total: totalCount,
      active: partners.filter((p) => p.isActive).length,
      inactive: partners.filter((p) => !p.isActive).length,
    }),
    [partners, totalCount],
  );

  return (
    <div className="prd-resumen prd-fadein">
      <div className="prd-kpi-grid">
        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">{icon}</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">
              {t(`${prefix}.kpi.total`, "Total registrados")}
            </p>
            <p className="pg-kpi-value">{String(stats.total)}</p>
          </div>
        </div>

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--success">
              <span className="material-symbols-outlined">check_circle</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">
              {t(`${prefix}.kpi.activePage`, "Activos (página)")}
            </p>
            <p className="pg-kpi-value">{stats.active}</p>
          </div>
        </div>

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--neutral">
              <span className="material-symbols-outlined">inventory</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">
              {t(`${prefix}.kpi.inactivePage`, "Inactivos (página)")}
            </p>
            <p className="pg-kpi-value">{stats.inactive}</p>
          </div>
        </div>
      </div>

      <div className="prd-resumen__bottom">
        <div className="prd-activity-card">
          <div className="prd-activity-card__head">
            <h3>{t("masterdata.resumen.activity", "Actividad reciente")}</h3>
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setActiveTab("listado")}
            >
              {t("masterdata.resumen.viewList", "Ver listado")}
            </ZHBtn>
          </div>
          {activity.length === 0 ? (
            <p className="prd-activity-empty">
              {t(
                "masterdata.resumen.noActivity",
                "Aún no hay acciones en esta sesión.",
              )}
            </p>
          ) : (
            <div className="prd-activity__list">
              {activity.map((item) => (
                <ActivityRow key={item.id} item={item} t={t} />
              ))}
            </div>
          )}
        </div>

        <div className="prd-quick-card">
          <h3>{t("masterdata.resumen.quickActions", "Acciones rápidas")}</h3>
          <button
            type="button"
            className="prd-quick-btn"
            onClick={() => setActiveTab("listado")}
          >
            <span className="material-symbols-outlined">view_list</span>
            {t(
              `${prefix}.tabList`,
              role === "customer" ? "Ver clientes" : "Ver proveedores",
            )}
          </button>
          <button
            type="button"
            className="prd-quick-btn"
            onClick={() => setActiveTab("nuevo")}
          >
            <span className="material-symbols-outlined">add_circle</span>
            {t(
              `${prefix}.tabNew`,
              role === "customer" ? "Nuevo cliente" : "Nuevo proveedor",
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
