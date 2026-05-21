import type { ReactNode } from 'react';
import { useI18n } from '../../i18n/i18n';
import type { FuncionalidadArbolDto } from '../../modules/superadmin/api/superAdminService';
import { MenuBuilderLibraryRow } from './MenuBuilderLibraryRow';

type Props = {
  crmUi: boolean;
  panelTitle?: string;
  crmLibraryStack?: ReactNode;
  availableLib: FuncionalidadArbolDto[];
  onPreviewForm?: (form: FuncionalidadArbolDto) => void;
};

export function MenuBuilderLibraryPanel({
  crmUi,
  panelTitle,
  crmLibraryStack,
  availableLib,
  onPreviewForm,
}: Props) {
  const { t } = useI18n();

  return (
    <aside className="menu-builder-panel menu-builder-panel--library">
      <header className="menu-builder-panel__head">
        <h4 className="menu-builder-panel__title">{panelTitle ?? t('superadmin.menuBuilder.libraryTitle')}</h4>
        <p className="menu-builder-panel__hint" title={t('superadmin.menuBuilder.libraryHint')}>
          {crmUi ? 'Arrastra y suelta hacia el árbol maestro.' : t('superadmin.menuBuilder.libraryHintShort')}
        </p>
        {crmUi && crmLibraryStack ? <div className="menu-builder-panel__crmStack">{crmLibraryStack}</div> : null}
      </header>
      <div className="menu-builder-panel__body">
        {availableLib.length === 0 ? (
          <p className="menu-preview-empty menu-preview-empty--library">{t('common.noData')}</p>
        ) : (
          <div className={`menu-builder-lib-stack${crmUi ? ' menu-builder-lib-stack--crm' : ''}`}>
            {availableLib.map((n) => (
              <MenuBuilderLibraryRow
                key={n.id}
                node={n}
                dense={crmUi}
                onPreview={crmUi ? onPreviewForm : undefined}
              />
            ))}
          </div>
        )}
      </div>
    </aside>
  );
}
