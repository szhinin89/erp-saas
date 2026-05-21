import type { ReactNode } from 'react';
import { MenuPreview } from '../menu-builder/MenuPreview';
import type { MenuPreviewLayout } from '../menu-builder/MenuPreview';
import type { MenuItem } from '../menu-builder/menuBuilderTypes';
import { formatMoney, planEmoji, type CrmLocalPlan } from './superAdminMenuBuilderUtils';

export type SuperAdminMenuBuilderCrmPreviewSectionProps = {
  previewData: MenuItem[];
  previewLayout: MenuPreviewLayout;
  setPreviewLayout: (layout: MenuPreviewLayout) => void;
  busy: boolean;
  savingAuto: boolean;
  activePlan: CrmLocalPlan | undefined;
  showAnnual: boolean;
  setShowAnnual: (v: boolean) => void;
  locale: string;
  planCardFeatures: string[];
};

export function SuperAdminMenuBuilderCrmPreviewSection({
  previewData,
  previewLayout,
  setPreviewLayout,
  busy,
  savingAuto,
  activePlan,
  showAnnual,
  setShowAnnual,
  locale,
  planCardFeatures,
}: SuperAdminMenuBuilderCrmPreviewSectionProps) {
  const locTag = locale === 'en' ? 'en-US' : 'es-ES';
  const priceLabel = activePlan ? formatMoney(showAnnual ? activePlan.priceYearly : activePlan.priceMonthly, 'USD', locTag) : '—';
  const cycleLabel = showAnnual ? '/año' : '/mes';

  const previewControls: ReactNode = (
    <div
      role="radiogroup"
      aria-label="Orientación del menú"
      style={{ display: 'flex', gap: 2, background: 'rgba(0,0,0,0.08)', borderRadius: 6, padding: 2 }}
    >
      {(['horizontal', 'vertical'] as const).map((layout) => {
        const active = previewLayout === layout;
        return (
          <button
            key={layout}
            type="button"
            role="radio"
            aria-checked={active}
            disabled={busy || savingAuto}
            onClick={() => setPreviewLayout(layout)}
            style={{
              display: 'flex', alignItems: 'center', gap: 3,
              padding: '2px 7px', borderRadius: 4,
              border: 'none', cursor: 'pointer', fontSize: 10,
              fontWeight: active ? 700 : 400,
              background: active ? '#fff' : 'transparent',
              color: active ? '#3a5f84' : '#64748b',
              boxShadow: active ? '0 1px 3px rgba(0,0,0,0.12)' : 'none',
              transition: 'all 0.15s',
            }}
          >
            <span className="material-symbols-outlined" style={{ fontSize: 11 }}>
              {layout === 'horizontal' ? 'dock_to_right' : 'view_sidebar'}
            </span>
            {layout === 'horizontal' ? 'Horizontal' : 'Vertical'}
          </button>
        );
      })}
    </div>
  );

  const planCard: ReactNode = (
    <div className="menu-plan-composer__planCard">
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8, marginBottom: 8 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span aria-hidden style={{ fontSize: 16 }}>{planEmoji(activePlan?.code ?? '')}</span>
          <span className="badge badge--blue badge--md" style={{ textTransform: 'uppercase' }}>
            {activePlan?.code ?? 'PLAN'}
          </span>
          <span style={{ fontSize: 13, fontWeight: 700, color: 'var(--color-text)' }}>
            {priceLabel}<span style={{ fontSize: 11, fontWeight: 400, color: 'var(--color-text-secondary)', marginLeft: 2 }}>{cycleLabel}</span>
          </span>
        </div>
        <div
          role="radiogroup"
          aria-label="Ciclo de facturación"
          style={{ display: 'flex', gap: 1, background: 'var(--color-surface-container)', borderRadius: 'var(--radius-md)', padding: 2 }}
        >
          {(['Mensual', 'Anual'] as const).map((label) => {
            const isAnnual = label === 'Anual';
            const active = showAnnual === isAnnual;
            return (
              <button
                key={label}
                type="button"
                role="radio"
                aria-checked={active}
                onClick={() => setShowAnnual(isAnnual)}
                style={{
                  padding: '2px 8px', borderRadius: 'var(--radius-sm)',
                  border: 'none', cursor: 'pointer', fontSize: 10,
                  fontWeight: active ? 700 : 400,
                  background: active ? 'var(--color-surface)' : 'transparent',
                  color: active ? 'var(--color-primary)' : 'var(--color-text-secondary)',
                  boxShadow: active ? 'var(--shadow-sm)' : 'none',
                  transition: 'all 0.15s',
                }}
              >
                {label}
              </button>
            );
          })}
        </div>
      </div>
      <p className="menu-plan-composer__planCardSub">{activePlan?.description || 'Ideal para equipos en crecimiento'}</p>
      <ul className="menu-plan-composer__planCardList">
        {planCardFeatures.length ? (
          planCardFeatures.map((name, idx) => (
            <li key={`${idx}-${name}`}>
              <span aria-hidden>✓</span> {name}
            </li>
          ))
        ) : (
          <li className="subtle">Añade carpetas o formularios al árbol para listarlos aquí.</li>
        )}
      </ul>
      <button type="button" className="zh-btn zh-btn--primary zh-btn--md menu-plan-composer__planCardCta" disabled aria-label="Seleccionar plan (solo demostración visual)">
        Seleccionar plan →
      </button>
    </div>
  );

  return (
    <div className="pg-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">visibility</span>
          <h3 className="pg-section-label">Vista empresa (previsualización)</h3>
          <span className="subtle" style={{ fontSize: 12, marginLeft: 'var(--space-2)' }}>
            Vista aproximada del menú según el plan activo.
          </span>
        </div>
        {previewControls}
      </div>
      <div className="pg-section-body" style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 'var(--space-4)' }}>
        <div style={{ width: '100%', maxWidth: 900 }}>
          <MenuPreview items={previewData} layout={previewLayout} />
        </div>
        <div style={{ width: '100%', maxWidth: 320 }}>
          {planCard}
        </div>
      </div>
    </div>
  );
}
