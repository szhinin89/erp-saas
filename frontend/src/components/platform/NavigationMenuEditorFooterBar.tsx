import { ZHBtn } from '../zh/ZHForm';
import { ZHInlineRowRight } from '../zh/ZHLayout';
import type { NavigationMenuEditorPanelState } from './useNavigationMenuEditorPanel';

type Props = Pick<
  NavigationMenuEditorPanelState,
  't' | 'saving' | 'creatingItem' | 'load' | 'handleSave'
>;

export function NavigationMenuEditorFooterBar({ t, saving, creatingItem, load, handleSave }: Props) {
  return (
    <div className="nm-footerBar">
      <ZHInlineRowRight>
        <ZHBtn type="button" variant="secondary" disabled={saving || creatingItem} onClick={() => void load()}>
          {t('superadmin.navigationMenu.reload')}
        </ZHBtn>
        <ZHBtn type="button" variant="primary" disabled={saving || creatingItem} onClick={() => void handleSave()}>
          {saving ? t('common.saving') : t('superadmin.navigationMenu.save')}
        </ZHBtn>
      </ZHInlineRowRight>
    </div>
  );
}
