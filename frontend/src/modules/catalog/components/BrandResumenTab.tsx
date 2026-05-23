import { useMemo } from 'react';
import { useI18n } from '../../../i18n/i18n';
import type { BrandItem } from '../api/catalogService';
import { useBrandUiStore, type BrandActivityItem } from '../store/brandUiStore';

interface Props {
  brands: BrandItem[];
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
  created:  { icon: 'add_circle',     cls: 'prd-activity__dot--green' },
  updated:  { icon: 'edit',           cls: 'prd-activity__dot--blue'  },
  disabled: { icon: 'visibility_off', cls: 'prd-activity__dot--red'   },
  enabled:  { icon: 'visibility',     cls: 'prd-activity__dot--green' },
};

const ACTION_KEYS: Record<string, string> = {
  created:  'brands.activity.action.created',
  updated:  'brands.activity.action.updated',
  disabled: 'brands.activity.action.disabled',
  enabled:  'brands.activity.action.enabled',
};

function ActivityRow({ item, t }: { item: BrandActivityItem; t: (k: string, fb?: string) => string }) {
  const meta = ACTIVITY_ICONS[item.action] ?? ACTIVITY_ICONS.updated;
  return (
    <div className="prd-activity__row">
      <div className={`prd-activity__dot ${meta.cls}`}>
        <span className="material-symbols-outlined" style={{ fontSize: 14 }}>{meta.icon}</span>
      </div>
      <div className="prd-activity__info">
        <span className="prd-activity__name">{item.itemName}</span>
        <span className="prd-activity__action">{t(ACTION_KEYS[item.action] ?? '', item.action)}</span>
      </div>
      <span className="prd-activity__time">{timeAgo(item.timestamp)}</span>
    </div>
  );
}

export function BrandResumenTab({ brands }: Props) {
  const { t } = useI18n();
  const activity    = useBrandUiStore((s) => s.recentActivity);
  const setActiveTab = useBrandUiStore((s) => s.setActiveTab);

  const stats = useMemo(() => {
    const countries     = new Set(brands.map((b) => b.countryOfOrigin).filter(Boolean));
    const manufacturers = new Set(brands.map((b) => b.manufacturer).filter(Boolean));
    return {
      total:         brands.length,
      active:        brands.filter((b) => b.isActive).length,
      inactive:      brands.filter((b) => !b.isActive).length,
      countries:     countries.size,
      manufacturers: manufacturers.size,
    };
  }, [brands]);

  return (
    <div className="cat-brand-resumen prd-fadein">

      {/* KPI grid */}
      <div className="cat-brand-kpi-grid">
        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">sell</span>
            </div>
            <span className="badge badge--green">
              <span className="material-symbols-outlined prd-kpi-trend-icon">trending_up</span>
              {t('common.active', 'Activo')}
            </span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('brands.kpi.total', 'Marcas')}</p>
            <p className="pg-kpi-value">{stats.total}</p>
          </div>
        </div>

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--error">
              <span className="material-symbols-outlined">block</span>
            </div>
            <span className="badge badge--red">{t('brands.qs.inactive', 'INACTIVAS')}</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('brands.kpi.inactive', 'Inactivas')}</p>
            <p className="pg-kpi-value pg-kpi-value--error">{stats.inactive}</p>
          </div>
        </div>

        <div className="pg-kpi cat-brand-kpi-wide">
          <div className="pg-kpi-top">
            <div>
              <p className="pg-kpi-label">{t('brands.kpi.countries', 'Países de origen')}</p>
              <p className="pg-kpi-value">{stats.countries}<span className="pg-kpi-unit">países</span></p>
            </div>
            <div className="prd-sparkline" aria-hidden>
              <span /><span /><span /><span className="prd-sparkline-bar--primary" />
            </div>
          </div>
        </div>
      </div>

      {/* Quick stats */}
      <div className="prd-quick-stats">
        {[
          { labelKey: 'brands.qs.active',        labelFb: 'ACTIVAS',      value: stats.active,        cls: 'prd-qs--green' },
          { labelKey: 'brands.qs.inactive',       labelFb: 'INACTIVAS',    value: stats.inactive,       cls: 'prd-qs--red'   },
          { labelKey: 'brands.qs.countries',      labelFb: 'PAÍSES',       value: stats.countries,      cls: 'prd-qs--blue'  },
          { labelKey: 'brands.qs.manufacturers',  labelFb: 'FABRICANTES',  value: stats.manufacturers,  cls: 'prd-qs--gray'  },
        ].map((s) => (
          <div key={s.labelKey} className={`prd-qs ${s.cls}`}>
            <span className="prd-qs__value">{s.value}</span>
            <span className="prd-qs__label">{t(s.labelKey, s.labelFb)}</span>
          </div>
        ))}
      </div>

      {/* Actividad reciente */}
      <div className="prd-activity">
        <div className="prd-activity__header">
          <span className="prd-activity__title">{t('brands.activity.title', '🕐 Actividad Reciente')}</span>
          <button
            type="button"
            className="zh-btn zh-btn--ghost zh-btn--sm"
            onClick={() => setActiveTab('listado')}
          >
            {t('brands.activity.viewAll', 'Ver todas')}
            <span className="material-symbols-outlined" style={{ fontSize: 14 }}>chevron_right</span>
          </button>
        </div>
        {activity.length === 0 ? (
          <div className="prd-activity__empty">
            <span className="material-symbols-outlined" style={{ fontSize: 32, color: 'var(--color-text-secondary)' }}>history</span>
            <p>{t('brands.activity.empty', 'No hay actividad reciente.')}</p>
            <p>{t('brands.activity.emptyHint', 'Las acciones aparecerán aquí.')}</p>
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
