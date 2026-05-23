import type { ReactNode } from 'react';
import { MenuPreview } from '../menu-builder/MenuPreview';
import type { MenuPreviewLayout } from '../menu-builder/MenuPreview';
import type { MenuItem } from '../menu-builder/menuBuilderTypes';
import { formatMoney, planEmoji, type CrmLocalPlan } from './platformMenuBuilderUtils';

export type PlatformMenuBuilderCrmPreviewSectionProps = {
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

export function PlatformMenuBuilderCrmPreviewSection({
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
}: PlatformMenuBuilderCrmPreviewSectionProps) {
  const locTag = locale === 'en' ? 'en-US' : 'es-ES';
  const priceLabel = activePlan ? formatMoney(showAnnual ? activePlan.priceYearly : activePlan.priceMonthly, 'USD', locTag) : '—';
  const cycleLabel = showAnnual ? '/año' : '/mes';

  const previewControls: ReactNode = (
    <div className="smb-layout-toggle" role="radiogroup" aria-label="Orientación del menú">
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
            className={`smb-layout-btn${active ? ' smb-layout-btn--active' : ''}`}
          >
            <span className={`material-symbols-outlined smb-layout-btn-icon`}>
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
      <div className="smb-plan-card-head">
        <div className="smb-plan-card-meta">
          <span className="smb-plan-emoji" aria-hidden>
            {planEmoji(activePlan?.code ?? '')}
          </span>
          <span className="badge badge--blue badge--md smb-plan-badge">{activePlan?.code ?? 'PLAN'}</span>
          <span className="smb-plan-price">
            {priceLabel}
            <span className="smb-plan-cycle">{cycleLabel}</span>
          </span>
        </div>
        <div className="smb-billing-toggle" role="radiogroup" aria-label="Ciclo de facturación">
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
                className={`smb-billing-btn${active ? ' smb-billing-btn--active' : ''}`}
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
      <button
        type="button"
        className="zh-btn zh-btn--primary zh-btn--md menu-plan-composer__planCardCta"
        disabled
        aria-label="Seleccionar plan (solo demostración visual)"
      >
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
          <span className="subtle smb-preview-hint">Vista aproximada del menú según el plan activo.</span>
        </div>
        {previewControls}
      </div>
      <div className="pg-section-body smb-preview-body">
        <div className="smb-preview-menu-wrap">
          <MenuPreview items={previewData} layout={previewLayout} />
        </div>
        <div className="smb-preview-plan-wrap">{planCard}</div>
      </div>
    </div>
  );
}
