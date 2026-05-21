import type { ReactNode } from 'react';
import { useI18n } from '../../i18n/i18n';
import { MenuPreview, type MenuPreviewLayout } from './MenuPreview';
import type { MenuItem } from './menuBuilderTypes';

type Props = {
  crmUi: boolean;
  panelTitle?: string;
  previewData: MenuItem[];
  previewLayout: MenuPreviewLayout;
  previewControls?: ReactNode;
  crmPreviewExtras?: ReactNode;
  crmPreviewExtrasBottom?: ReactNode;
};

export function MenuBuilderPreviewPanel({
  crmUi,
  panelTitle,
  previewData,
  previewLayout,
  previewControls,
  crmPreviewExtras,
  crmPreviewExtrasBottom,
}: Props) {
  const { t } = useI18n();

  return (
    <aside className="menu-builder-panel menu-builder-panel--preview">
      {!crmUi ? (
        <header className="menu-builder-panel__head">
          <h4 className="menu-builder-panel__title">{panelTitle ?? t('superadmin.menuBuilder.livePreview')}</h4>
          <p className="menu-builder-panel__hint">{t('superadmin.menuBuilder.previewHintShort')}</p>
        </header>
      ) : null}
      <div className="menu-builder-panel__body">
        {crmUi && crmPreviewExtras ? <div className="menu-builder-panel__crmPreviewTop">{crmPreviewExtras}</div> : null}
        <MenuPreview items={previewData} layout={previewLayout} controls={previewControls} />
        {crmUi && crmPreviewExtrasBottom ? (
          <div className="menu-builder-panel__crmPreviewBottom">{crmPreviewExtrasBottom}</div>
        ) : null}
      </div>
    </aside>
  );
}
