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
        <h3 id="nm-create-title">{t('superadmin.navigationMenu.createItemTitle')}</h3>
        <p className="nm-createHint">
          {createTarget.parentItemId
            ? t('superadmin.navigationMenu.createItemHintChild')
            : t('superadmin.navigationMenu.createItemHintRoot')}
        </p>
        <form onSubmit={(e: FormEvent) => void handleCreateNavItemSubmit(e)}>
          <div className="nm-createField">
            <label htmlFor="nm-create-label">{t('superadmin.navigationMenu.createItemDisplayLabel')}</label>
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
            <label htmlFor="nm-create-route">{t('superadmin.navigationMenu.createItemRoutePath')}</label>
            <input
              id="nm-create-route"
              value={createRoutePath}
              onChange={(e) => setCreateRoutePath(e.target.value)}
              disabled={creatingItem}
              autoComplete="off"
            />
          </div>
          <div className="nm-createField">
            <label htmlFor="nm-create-mod">{t('superadmin.navigationMenu.createItemModuleKey')}</label>
            <input
              id="nm-create-mod"
              value={createModuleKey}
              onChange={(e) => setCreateModuleKey(e.target.value)}
              disabled={creatingItem}
              autoComplete="off"
            />
          </div>
          <div className="nm-createField">
            <label htmlFor="nm-create-perm">{t('superadmin.navigationMenu.createItemPermissionKey')}</label>
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
              {t('superadmin.navigationMenu.createItemCancel')}
            </ZHBtn>
            <ZHBtn type="submit" variant="primary" disabled={creatingItem}>
              {creatingItem ? t('superadmin.navigationMenu.createItemCreating') : t('superadmin.navigationMenu.createItemSubmit')}
            </ZHBtn>
          </div>
        </form>
      </dialog>
    </div>
  );
}
