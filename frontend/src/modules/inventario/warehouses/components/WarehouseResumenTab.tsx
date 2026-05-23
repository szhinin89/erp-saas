import { useMemo } from 'react';
import { useI18n } from '../../../../i18n/i18n';
import type { WarehouseDto } from '../api/warehouseService';
import { useWarehouseUiStore, type WhActivityItem } from '../store/warehouseUiStore';

interface Props {
  warehouses: WarehouseDto[];
}

function timeAgo(date: Date): string {
  const diff = Date.now() - date.getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'ahora mismo';
  if (mins < 60) return `hace ${mins} min`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `hace ${hrs} h`;
  return `hace ${Math.floor(hrs / 24)} d`;
}

const ACTIVITY_ICONS: Record<string, { icon: string; cls: string }> = {
  created:  { icon: 'add_circle',    cls: 'bod-activity__dot--green' },
  updated:  { icon: 'edit',          cls: 'bod-activity__dot--blue'  },
  disabled: { icon: 'visibility_off',cls: 'bod-activity__dot--red'   },
  enabled:  { icon: 'visibility',    cls: 'bod-activity__dot--green' },
};

const ACTION_LABEL_KEYS: Record<string, string> = {
  created:  'warehouses.activity.action.created',
  updated:  'warehouses.activity.action.updated',
  disabled: 'warehouses.activity.action.disabled',
  enabled:  'warehouses.activity.action.enabled',
};

function ActivityRow({ item, t }: { item: WhActivityItem; t: (k: string, fb?: string) => string }) {
  const meta = ACTIVITY_ICONS[item.action] ?? ACTIVITY_ICONS.updated;
  return (
    <div className="bod-activity__row">
      <div className={`bod-activity__dot ${meta.cls}`}>
        <span className="material-symbols-outlined" style={{ fontSize: 14 }}>{meta.icon}</span>
      </div>
      <div className="bod-activity__info">
        <span className="bod-activity__name">{item.warehouseName}</span>
        <span className="bod-activity__action">{t(ACTION_LABEL_KEYS[item.action] ?? '', item.action)}</span>
      </div>
      <span className="bod-activity__time">{timeAgo(item.timestamp)}</span>
    </div>
  );
}

export function WarehouseResumenTab({ warehouses }: Props) {
  const { t } = useI18n();
  const activity    = useWarehouseUiStore((s) => s.recentActivity);
  const setActiveTab = useWarehouseUiStore((s) => s.setActiveTab);

  const stats = useMemo(() => {
    const storageTypes = new Set(warehouses.map((w) => w.storageType).filter(Boolean));
    return {
      total:    warehouses.length,
      active:   warehouses.filter((w) => w.isActive).length,
      inactive: warehouses.filter((w) => !w.isActive).length,
      branches: new Set(warehouses.map((w) => w.branchId)).size,
      types:    storageTypes.size,
    };
  }, [warehouses]);

  return (
    <div className="bod-resumen prd-fadein">

      {/* ── KPI grid ─────────────────────────────────────── */}
      <div className="bod-kpi-grid">
        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">warehouse</span>
            </div>
            <span className="badge badge--green">
              <span className="material-symbols-outlined prd-kpi-trend-icon">trending_up</span>
              {t('common.active', 'Activo')}
            </span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('warehouses.kpi.total', 'Total Bodegas')}</p>
            <p className="pg-kpi-value">{stats.total}</p>
          </div>
        </div>

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--error">
              <span className="material-symbols-outlined">block</span>
            </div>
            <span className="badge badge--red">{t('warehouses.qs.inactive', 'INACTIVAS')}</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('warehouses.kpi.inactive', 'Inactivas')}</p>
            <p className="pg-kpi-value pg-kpi-value--error">{stats.inactive}</p>
          </div>
        </div>

        <div className="pg-kpi bod-kpi-wide">
          <div className="pg-kpi-top">
            <div>
              <p className="pg-kpi-label">{t('warehouses.kpi.branches', 'Sucursales con Bodega')}</p>
              <p className="pg-kpi-value">{stats.branches}<span className="pg-kpi-unit">suc.</span></p>
            </div>
            <div className="prd-sparkline" aria-hidden>
              <span /><span /><span /><span className="prd-sparkline-bar--primary" />
            </div>
          </div>
        </div>
      </div>

      {/* ── Quick stats ───────────────────────────────────── */}
      <div className="prd-quick-stats">
        {[
          { labelKey: 'warehouses.qs.active',   labelFb: 'ACTIVAS',    value: stats.active,   cls: 'prd-qs--green' },
          { labelKey: 'warehouses.qs.inactive',  labelFb: 'INACTIVAS',  value: stats.inactive, cls: 'prd-qs--red'   },
          { labelKey: 'warehouses.qs.branches',  labelFb: 'SUCURSALES', value: stats.branches, cls: 'prd-qs--blue'  },
          { labelKey: 'warehouses.qs.types',     labelFb: 'TIPOS',      value: stats.types,    cls: 'prd-qs--gray'  },
        ].map((s) => (
          <div key={s.labelKey} className={`prd-qs ${s.cls}`}>
            <span className="prd-qs__value">{s.value}</span>
            <span className="prd-qs__label">{t(s.labelKey, s.labelFb)}</span>
          </div>
        ))}
      </div>

      {/* ── Actividad reciente ────────────────────────────── */}
      <div className="prd-activity">
        <div className="prd-activity__header">
          <span className="prd-activity__title">{t('warehouses.activity.title', '🕐 Actividad Reciente')}</span>
          <button
            type="button"
            className="zh-btn zh-btn--ghost zh-btn--sm"
            onClick={() => setActiveTab('listado')}
          >
            {t('warehouses.activity.viewAll', 'Ver todas')}
            <span className="material-symbols-outlined" style={{ fontSize: 14 }}>chevron_right</span>
          </button>
        </div>
        {activity.length === 0 ? (
          <div className="prd-activity__empty">
            <span className="material-symbols-outlined" style={{ fontSize: 32, color: 'var(--color-text-secondary)' }}>history</span>
            <p>{t('warehouses.activity.empty', 'No hay actividad reciente.')}</p>
            <p>{t('warehouses.activity.emptyHint', 'Las acciones aparecerán aquí.')}</p>
          </div>
        ) : (
          <div className="prd-activity__list">
            {activity.map((item) => (
              <ActivityRow key={item.id} item={item} t={t} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
