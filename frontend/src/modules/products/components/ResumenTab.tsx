import { useMemo } from 'react';
import type { Product } from '../../../types/product';
import type { ProductCatalogs } from '../hooks/useProducts';
import { useProductUiStore, type ActivityItem } from '../store/productUiStore';
import { useI18n } from '../../../i18n/i18n';

interface Props {
  products: Product[];
  catalogs: ProductCatalogs | null | undefined;
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
  created:  { icon: 'add_circle',    cls: 'prd-activity__dot--green' },
  updated:  { icon: 'edit',          cls: 'prd-activity__dot--blue'  },
  disabled: { icon: 'visibility_off',cls: 'prd-activity__dot--red'   },
  enabled:  { icon: 'visibility',    cls: 'prd-activity__dot--green' },
};

const ACTION_LABEL_KEYS: Record<string, string> = {
  created:  'products.activity.action.created',
  updated:  'products.activity.action.updated',
  disabled: 'products.activity.action.disabled',
  enabled:  'products.activity.action.enabled',
};

function ActivityRow({ item, t }: { item: ActivityItem; t: (k: string, fb?: string) => string }) {
  const meta = ACTIVITY_ICONS[item.action] ?? ACTIVITY_ICONS.updated;
  return (
    <div className="prd-activity__row">
      <div className={`prd-activity__dot ${meta.cls}`}>
        <span className="material-symbols-outlined" style={{ fontSize: 14 }}>
          {meta.icon}
        </span>
      </div>
      <div className="prd-activity__info">
        <span className="prd-activity__name">{item.productName}</span>
        <span className="prd-activity__action">{t(ACTION_LABEL_KEYS[item.action] ?? '', item.action)}</span>
      </div>
      <span className="prd-activity__time">{timeAgo(item.timestamp)}</span>
    </div>
  );
}

export function ResumenTab({ products }: Props) {
  const { t } = useI18n();
  const activity = useProductUiStore((s) => s.recentActivity);
  const setActiveTab = useProductUiStore((s) => s.setActiveTab);

  const stats = useMemo(() => {
    const total    = products.length;
    const active   = products.filter((p) => p.isActive).length;
    const inactive = products.filter((p) => !p.isActive).length;
    const services = products.filter((p) => p.isService).length;
    const goods    = products.filter((p) => !p.isService && p.isActive).length;
    const lowStock = inactive; // proxy until real stock API
    return { total, active, inactive, services, goods, lowStock };
  }, [products]);

  return (
    <div className="prd-resumen prd-fadein">

      {/* ── KPI bento grid ─────────────────────────────────── */}
      <div className="prd-kpi-grid">

        {/* Total SKUs */}
        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">inventory_2</span>
            </div>
            <span className="badge badge--green">
              <span className="material-symbols-outlined prd-kpi-trend-icon">trending_up</span>
              +12%
            </span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('products.kpi.totalSkus', 'Total SKUs')}</p>
            <p className="pg-kpi-value">{stats.total.toLocaleString()}</p>
          </div>
        </div>

        {/* Bajo Stock */}
        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--error">
              <span className="material-symbols-outlined">warning</span>
            </div>
            <span className="badge badge--red">{t('products.kpi.urgent', 'Urgente')}</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('products.kpi.lowStock', 'Bajo Stock')}</p>
            <p className="pg-kpi-value pg-kpi-value--error">{stats.lowStock}</p>
          </div>
        </div>

        {/* Valorización */}
        <div className="pg-kpi prd-kpi-wide">
          <div className="pg-kpi-top">
            <div>
              <p className="pg-kpi-label">{t('products.kpi.valuation', 'Valorización de Inventario')}</p>
              <p className="pg-kpi-value">—<span className="pg-kpi-unit">USD</span></p>
            </div>
            <div className="prd-sparkline" aria-hidden>
              <span /><span /><span /><span className="prd-sparkline-bar--primary" />
            </div>
          </div>
        </div>
      </div>

      {/* ── Quick stats ────────────────────────────────────── */}
      <div className="prd-quick-stats">
        {[
          { labelKey: 'products.qs.active',   labelFb: 'ACTIVOS',   value: stats.active,   cls: 'prd-qs--green' },
          { labelKey: 'products.qs.inactive', labelFb: 'INACTIVOS', value: stats.inactive, cls: 'prd-qs--red'   },
          { labelKey: 'products.qs.services', labelFb: 'SERVICIOS', value: stats.services, cls: 'prd-qs--blue'  },
          { labelKey: 'products.qs.goods',    labelFb: 'BIENES',    value: stats.goods,    cls: 'prd-qs--gray'  },
        ].map((s) => (
          <div key={s.labelKey} className={`prd-qs ${s.cls}`}>
            <span className="prd-qs__value">{s.value}</span>
            <span className="prd-qs__label">{t(s.labelKey, s.labelFb)}</span>
          </div>
        ))}
      </div>

      {/* ── Actividad reciente ─────────────────────────────── */}
      <div className="prd-activity">
        <div className="prd-activity__header">
          <span className="prd-activity__title">{t('products.activity.title', '🕐 Actividad Reciente')}</span>
          <button
            type="button"
            className="zh-btn zh-btn--ghost zh-btn--sm"
            onClick={() => setActiveTab('listado')}
          >
            {t('products.activity.viewAll', 'Ver todos')}
            <span className="material-symbols-outlined" style={{ fontSize: 14 }}>chevron_right</span>
          </button>
        </div>

        {activity.length === 0 ? (
          <div className="prd-activity__empty">
            <span className="material-symbols-outlined" style={{ fontSize: 32, color: 'var(--color-text-secondary)' }}>
              history
            </span>
            <p>{t('products.activity.empty', 'No hay actividad reciente.')}</p>
            <p>{t('products.activity.emptyHint', 'Las acciones aparecerán aquí.')}</p>
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
