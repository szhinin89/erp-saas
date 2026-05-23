import { LoadingState } from '../PageShell';
import { ZHCard } from '../zh/ZHCard';
import { ZHPageNotice } from '../zh/ZHPageNotice';
import { ZHConfirmModal } from '../zh/ZHConfirmModal';
import { countNavSubtreeNodes } from '../../modules/platform/navigationMenuEditorModel';
import { NavigationMenuCreateItemDialog } from './NavigationMenuCreateItemDialog';
import { NavigationMenuEditItemDialog } from './NavigationMenuEditItemDialog';
import { NavigationMenuEditorFooterBar } from './NavigationMenuEditorFooterBar';
import { NavigationMenuEditorSplitAside } from './NavigationMenuEditorSplitAside';
import { NavigationMenuEditorTreePanel } from './NavigationMenuEditorTreePanel';
import { useNavigationMenuEditorPanel } from './useNavigationMenuEditorPanel';
import '../../pages/PlatformNavMenuPage.css';

export type NavigationMenuEditorPanelProps = {
  /** Vista 2:1: árbol a la izquierda, propiedades + vista previa a la derecha. */
  splitWorkspace?: boolean;
};

/**
 * Editor global del menú principal (grupos de barra + ítems). Misma API que la antigua página solo platform.
 */
export function NavigationMenuEditorPanel({ splitWorkspace = false }: NavigationMenuEditorPanelProps) {
  const state = useNavigationMenuEditorPanel(splitWorkspace);
  const {
    t,
    isPlatformOperator,
    hasSelectedSubscriber,
    loading,
    error,
    success,
    menu,
    selection,
    deleteOpen,
    deletingItem,
    setDeleteOpen,
    handleDeleteNavItem,
  } = state;

  if (!isPlatformOperator || hasSelectedSubscriber) {
    return <p className="zh-help-text subtle">{t('platform.sectionLoadHint')}</p>;
  }

  return (
    <>
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
      {success ? <ZHPageNotice variant="success" message={t('platform.navigationMenu.saved')} /> : null}

      <ZHCard>
        {loading ? (
          <LoadingState />
        ) : menu ? (
          <div className={`sa-navmenu-page${splitWorkspace ? ' sa-navmenu-page--split' : ''}`}>
            <NavigationMenuEditorTreePanel {...state} splitWorkspace={splitWorkspace} />

            {splitWorkspace ? <NavigationMenuEditorSplitAside {...state} /> : null}

            <NavigationMenuEditorFooterBar {...state} />

            <NavigationMenuCreateItemDialog {...state} />

            <NavigationMenuEditItemDialog {...state} splitWorkspace={splitWorkspace} />

            {deleteOpen && selection ? (
              <ZHConfirmModal
                title={t('platform.navigationMenu.deleteItemTitle')}
                message={
                  <>
                    {t('platform.navigationMenu.deleteItemConfirmPrefix')}{' '}
                    <strong>{countNavSubtreeNodes(selection.item)}</strong>{' '}
                    {t('platform.navigationMenu.deleteItemConfirmSuffix')}
                  </>
                }
                loading={deletingItem}
                onCancel={() => (deletingItem ? undefined : setDeleteOpen(false))}
                onConfirm={() => void handleDeleteNavItem()}
              />
            ) : null}
          </div>
        ) : (
          <div className="empty-state">{t('platform.sectionLoadHint')}</div>
        )}
      </ZHCard>
    </>
  );
}
