import { useI18n } from '../../i18n/i18n';
import type { MenuPreviewLayout } from './MenuPreview';
import type { MenuBuilderViewMode } from './menuBuilderComponentTypes';

type Props = {
  viewMode: MenuBuilderViewMode;
  onViewModeChange: (mode: MenuBuilderViewMode) => void;
  previewLayout: MenuPreviewLayout;
  onPreviewLayoutChange: (layout: MenuPreviewLayout) => void;
  showPreview: boolean;
};

export function MenuBuilderWorkspaceToolbar({
  viewMode,
  onViewModeChange,
  previewLayout,
  onPreviewLayoutChange,
  showPreview,
}: Props) {
  const { t } = useI18n();

  return (
    <div className="menu-builder-toolbar" role="toolbar" aria-label={t('superadmin.menuBuilder.visualMode')}>
      <span className="menu-builder-toolbar__label">{t('superadmin.menuBuilder.visualMode')}</span>
      <button
        type="button"
        className={`zh-btn zh-btn--sm ${viewMode === 'split' ? 'zh-btn--primary' : 'zh-btn--ghost'}`}
        onClick={() => onViewModeChange('split')}
      >
        {t('superadmin.menuBuilder.modeSplit')}
      </button>
      <button
        type="button"
        className={`zh-btn zh-btn--sm ${viewMode === 'editor' ? 'zh-btn--primary' : 'zh-btn--ghost'}`}
        onClick={() => onViewModeChange('editor')}
      >
        {t('superadmin.menuBuilder.modeEditor')}
      </button>
      <button
        type="button"
        className={`zh-btn zh-btn--sm ${viewMode === 'preview' ? 'zh-btn--primary' : 'zh-btn--ghost'}`}
        onClick={() => onViewModeChange('preview')}
      >
        {t('superadmin.menuBuilder.modePreview')}
      </button>
      {showPreview ? (
        <>
          <span className="menu-builder-toolbar__sep" aria-hidden />
          <span className="menu-builder-toolbar__label">{t('superadmin.menuBuilder.previewLayout')}</span>
          <button
            type="button"
            className={`zh-btn zh-btn--sm ${previewLayout === 'vertical' ? 'zh-btn--secondary' : 'zh-btn--ghost'}`}
            onClick={() => onPreviewLayoutChange('vertical')}
          >
            {t('superadmin.menuBuilder.layoutVertical')}
          </button>
          <button
            type="button"
            className={`zh-btn zh-btn--sm ${previewLayout === 'horizontal' ? 'zh-btn--secondary' : 'zh-btn--ghost'}`}
            onClick={() => onPreviewLayoutChange('horizontal')}
          >
            {t('superadmin.menuBuilder.layoutHorizontal')}
          </button>
        </>
      ) : null}
    </div>
  );
}
