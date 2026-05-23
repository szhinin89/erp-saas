import type { FormEvent } from 'react';
import { ZHBtn } from '../zh/ZHForm';
import type { NavigationMenuEditorPanelState } from './useNavigationMenuEditorPanel';

type Props = Pick<
  NavigationMenuEditorPanelState,
  | 't'
  | 'createTarget'
  | 'createDisplayLabel'
  | 'setCreateDisplayLabel'
  | 'createRoutePath'
  | 'setCreateRoutePath'
  | 'createModuleKey'
  | 'setCreateModuleKey'
  | 'createPermissionKey'
  | 'setCreatePermissionKey'
  | 'creatingItem'
  | 'closeCreateNavItem'
  | 'handleCreateNavItemSubmit'
>;

export function NavigationMenuCreateItemDialog({
  t,
  createTarget,
  createDisplayLabel,
  setCreateDisplayLabel,
  createRoutePath,
  setCreateRoutePath,
  createModuleKey,
  setCreateModuleKey,
  createPermissionKey,
  setCreatePermissionKey,
  creatingItem,
  closeCreateNavItem,
  handleCreateNavItemSubmit,
}: Props) {
  if (!createTarget) return null;

  return (
    <div
      className="nm-createBackdrop"
      role="presentation"
      onClick={(ev) => {
        if (ev.target === ev.currentTarget) closeCreateNavItem();
      }}
    >
      <dialog className="nm-createDialog" open aria-labelledby="nm-create-title">
        <h3 id="nm-create-title">{t('platform.navigationMenu.createItemTitle')}</h3>
        <p className="nm-createHint">
          {createTarget.parentItemId
            ? t('platform.navigationMenu.createItemHintChild')
            : t('platform.navigationMenu.createItemHintRoot')}
        </p>
        <form onSubmit={(e: FormEvent) => void handleCreateNavItemSubmit(e)}>
          <div className="nm-createField">
            <label htmlFor="nm-create-label">{t('platform.navigationMenu.createItemDisplayLabel')}</label>
            <input
              id="nm-create-label"
              value={createDisplayLabel}
              onChange={(e) => setCreateDisplayLabel(e.target.value)}
              disabled={creatingItem}
              autoComplete="off"
              maxLength={200}
            />
          </div>
          <div className="nm-createField">
            <label htmlFor="nm-create-route">{t('platform.navigationMenu.createItemRoutePath')}</label>
            <input
              id="nm-create-route"
              value={createRoutePath}
              onChange={(e) => setCreateRoutePath(e.target.value)}
              disabled={creatingItem}
              autoComplete="off"
            />
          </div>
          <div className="nm-createField">
            <label htmlFor="nm-create-mod">{t('platform.navigationMenu.createItemModuleKey')}</label>
            <input
              id="nm-create-mod"
              value={createModuleKey}
              onChange={(e) => setCreateModuleKey(e.target.value)}
              disabled={creatingItem}
              autoComplete="off"
            />
          </div>
          <div className="nm-createField">
            <label htmlFor="nm-create-perm">{t('platform.navigationMenu.createItemPermissionKey')}</label>
            <input
              id="nm-create-perm"
              value={createPermissionKey}
              onChange={(e) => setCreatePermissionKey(e.target.value)}
              disabled={creatingItem}
              autoComplete="off"
            />
          </div>
          <div className="nm-createActions">
            <ZHBtn type="button" variant="secondary" disabled={creatingItem} onClick={closeCreateNavItem}>
              {t('platform.navigationMenu.createItemCancel')}
            </ZHBtn>
            <ZHBtn type="submit" variant="primary" disabled={creatingItem}>
              {creatingItem ? t('platform.navigationMenu.createItemCreating') : t('platform.navigationMenu.createItemSubmit')}
            </ZHBtn>
          </div>
        </form>
      </dialog>
    </div>
  );
}
