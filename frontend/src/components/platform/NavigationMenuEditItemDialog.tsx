import type { FormEvent } from 'react';
import { ZHBtn } from '../zh/ZHForm';
import type { NavigationMenuEditorPanelState } from './useNavigationMenuEditorPanel';

type Props = Pick<
  NavigationMenuEditorPanelState,
  | 't'
  | 'selection'
  | 'editOpen'
  | 'editDisplayLabel'
  | 'setEditDisplayLabel'
  | 'editRoutePath'
  | 'setEditRoutePath'
  | 'editModuleKey'
  | 'setEditModuleKey'
  | 'editPermissionKey'
  | 'setEditPermissionKey'
  | 'editFeatureId'
  | 'setEditFeatureId'
  | 'savingEdit'
  | 'closeEditNavItem'
  | 'handleEditNavItemSubmit'
> & {
  splitWorkspace: boolean;
};

export function NavigationMenuEditItemDialog({
  t,
  selection,
  editOpen,
  splitWorkspace,
  editDisplayLabel,
  setEditDisplayLabel,
  editRoutePath,
  setEditRoutePath,
  editModuleKey,
  setEditModuleKey,
  editPermissionKey,
  setEditPermissionKey,
  editFeatureId,
  setEditFeatureId,
  savingEdit,
  closeEditNavItem,
  handleEditNavItemSubmit,
}: Props) {
  if (!editOpen || !selection || splitWorkspace) return null;

  return (
    <div
      className="nm-createBackdrop"
      role="presentation"
      onClick={(ev) => {
        if (ev.target === ev.currentTarget) closeEditNavItem();
      }}
    >
      <dialog className="nm-createDialog" open aria-labelledby="nm-edit-title">
        <h3 id="nm-edit-title">{t('platform.navigationMenu.editItemTitle')}</h3>
        <form onSubmit={(e: FormEvent) => void handleEditNavItemSubmit(e)}>
          <div className="nm-createField">
            <label htmlFor="nm-edit-label">{t('platform.navigationMenu.createItemDisplayLabel')}</label>
            <input
              id="nm-edit-label"
              value={editDisplayLabel}
              onChange={(e) => setEditDisplayLabel(e.target.value)}
              disabled={savingEdit}
              autoComplete="off"
              maxLength={200}
            />
          </div>
          <div className="nm-createField">
            <label htmlFor="nm-edit-route">{t('platform.navigationMenu.createItemRoutePath')}</label>
            <input
              id="nm-edit-route"
              value={editRoutePath}
              onChange={(e) => setEditRoutePath(e.target.value)}
              disabled={savingEdit}
              autoComplete="off"
            />
          </div>
          <div className="nm-createField">
            <label htmlFor="nm-edit-mod">{t('platform.navigationMenu.createItemModuleKey')}</label>
            <input
              id="nm-edit-mod"
              value={editModuleKey}
              onChange={(e) => setEditModuleKey(e.target.value)}
              disabled={savingEdit}
              autoComplete="off"
            />
          </div>
          <div className="nm-createField">
            <label htmlFor="nm-edit-perm">{t('platform.navigationMenu.createItemPermissionKey')}</label>
            <input
              id="nm-edit-perm"
              value={editPermissionKey}
              onChange={(e) => setEditPermissionKey(e.target.value)}
              disabled={savingEdit}
              autoComplete="off"
            />
          </div>
          <div className="nm-createField">
            <label htmlFor="nm-edit-feat">{t('platform.navigationMenu.editFeatureId')}</label>
            <input
              id="nm-edit-feat"
              value={editFeatureId}
              onChange={(e) => setEditFeatureId(e.target.value)}
              disabled={savingEdit}
              autoComplete="off"
              placeholder={t('platform.navigationMenu.editFeatureIdHint')}
            />
          </div>
          <div className="nm-createActions">
            <ZHBtn type="button" variant="secondary" disabled={savingEdit} onClick={closeEditNavItem}>
              {t('platform.navigationMenu.createItemCancel')}
            </ZHBtn>
            <ZHBtn type="submit" variant="primary" disabled={savingEdit}>
              {savingEdit ? t('common.saving') : t('platform.navigationMenu.saveItem')}
            </ZHBtn>
          </div>
        </form>
      </dialog>
    </div>
  );
}
