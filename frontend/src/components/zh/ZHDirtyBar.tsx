import { useI18n } from '../../i18n/i18n';
import { ZHBtn } from './ZHForm';

export function ZHDirtyBar(props: {
  visible: boolean;
  message?: string;
  onDiscard?: () => void;
  onSave?: () => void;
  discardLabel?: string;
  saveLabel?: string;
  loading?: boolean;
}) {
  const { t } = useI18n();
  const { visible, message, onDiscard, onSave, discardLabel, saveLabel, loading } = props;
  if (!visible) return null;

  return (
    <div className="zh-dirtybar" role="status" aria-live="polite">
      <div className="zh-dirtybar-text">
        {message ?? t('common.unsavedChanges')}
      </div>
      <div className="zh-dirtybar-actions">
        {onDiscard ? (
          <ZHBtn variant="ghost" size="md" type="button" onClick={onDiscard} disabled={loading}>
            {discardLabel ?? t('common.discard')}
          </ZHBtn>
        ) : null}
        {onSave ? (
          <ZHBtn variant="primary" size="md" type="button" onClick={onSave} disabled={loading}>
            {saveLabel ?? t('common.saveChanges')}
          </ZHBtn>
        ) : null}
      </div>
    </div>
  );
}

